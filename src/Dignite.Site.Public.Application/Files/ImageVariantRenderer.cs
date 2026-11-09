using System;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Site.Public.Files;

/// <summary>
/// Renders the image the public read endpoint serves from a stored picture: turned upright, resized when a size is
/// asked, and encoded as WebP when the caller takes it. Everything is rendered from the stored picture in one decode
/// and one encode, so a served image is never a re-encode of a re-encode.
/// <para>
/// <b>Formats.</b> JPEG, PNG and WebP are rendered; anything else (a GIF) is not, and is served as stored. With WebP
/// a JPEG is encoded lossy at <see cref="SkiaSharpResizerOptions.Quality"/>, and a PNG lossless: a PNG is usually a
/// screenshot, a logo or a drawing with text, whose edges lossy WebP blurs, since it always stores colour at half
/// the resolution of brightness. Lossless WebP keeps every pixel and the transparency. Without WebP a picture keeps
/// its own format, as ABP's resizer does.
/// </para>
/// <para>
/// <b>Upright.</b> A camera writes a photo sideways and records how to turn it in its EXIF orientation, which a
/// browser applies to the stored file. Re-encoding drops the EXIF, so the orientation is applied to the pixels here.
/// ABP's SkiaSharp resizer does not (<c>SKBitmap.Decode</c> ignores it), which is why the read side no longer
/// goes through <c>IImageResizer</c>.
/// </para>
/// <para>
/// A size is applied as ABP's <c>ImageResizeMode.Crop</c> does, so an address resized before renders the same
/// box: one side given, scaled to it with the other in proportion; both given, scaled to cover the box and
/// cropped to it around the centre. The sampling is <see cref="SkiaSharpResizerOptions.SKSamplingOptions"/>.
/// </para>
/// </summary>
public class ImageVariantRenderer : ITransientDependency
{
    public const string JpegMimeType = "image/jpeg";
    public const string PngMimeType = "image/png";
    public const string WebpMimeType = "image/webp";

    /// <summary>
    /// Lossless WebP's effort, 0-100: what libwebp spends to find a smaller encoding of the same pixels; the image
    /// is identical whatever the value. On a 3840 x 2160 PNG, 25 took 0.6 s where libwebp's default of 75 took 3.9 s,
    /// for a file 1% larger.
    /// </summary>
    protected const float LosslessWebpEffort = 25;

    protected SkiaSharpResizerOptions Options { get; }

    public ImageVariantRenderer(IOptions<SkiaSharpResizerOptions> options)
    {
        Options = options.Value;
    }

    /// <summary>Whether a picture of <paramref name="mimeType"/> is served as WebP to a caller that takes it.</summary>
    public static bool ConvertsToWebp(string? mimeType)
    {
        return string.Equals(mimeType, JpegMimeType, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mimeType, PngMimeType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The image to serve for the picture in <paramref name="source"/>, or <c>null</c> to serve the stored picture
    /// as it is: a format this does not render, or - with no size asked - a WebP no smaller than the stored file.
    /// The caller has bounded the picture's size from its header before calling.
    /// </summary>
    /// <exception cref="InvalidOperationException">The picture's pixels cannot be decoded, or the image not encoded.</exception>
    public virtual ImageVariant? Render(byte[] source, int? width, int? height, bool acceptWebp)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data);
        if (codec == null)
        {
            return null;
        }

        var sourceFormat = codec.EncodedFormat;
        if (sourceFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            return null;
        }

        var resize = (width ?? 0) > 0 || (height ?? 0) > 0;
        var toWebp = acceptWebp && (sourceFormat is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png);
        if (!resize && !toWebp)
        {
            return null;
        }

        // 32-bit pixels whatever the source (a 16-bit or greyscale PNG), which every encoder takes; premultiplied
        // unless opaque, which a canvas draws.
        var info = codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        using var decoded = SKBitmap.Decode(codec, info) ?? throw new InvalidOperationException("The picture's pixels could not be decoded.");
        var upright = Orient(decoded, codec.EncodedOrigin);
        try
        {
            var resized = resize ? Resize(upright, width ?? 0, height ?? 0) : upright;
            try
            {
                var (bytes, mimeType) = Encode(resized, sourceFormat, toWebp);
                if (!resize && bytes.Length >= source.Length)
                {
                    return null;
                }

                return new ImageVariant(bytes, mimeType);
            }
            finally
            {
                if (!ReferenceEquals(resized, upright))
                {
                    resized.Dispose();
                }
            }
        }
        finally
        {
            if (!ReferenceEquals(upright, decoded))
            {
                upright.Dispose();
            }
        }
    }

    protected virtual (byte[] Bytes, string MimeType) Encode(SKBitmap bitmap, SKEncodedImageFormat sourceFormat, bool toWebp)
    {
        using var pixmap = bitmap.PeekPixels() ?? throw new InvalidOperationException("The image's pixels are not readable.");

        SKData? encoded;
        string mimeType;
        if (toWebp)
        {
            encoded = pixmap.Encode(sourceFormat == SKEncodedImageFormat.Png
                ? new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, LosslessWebpEffort)
                : new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Options.Quality));
            mimeType = WebpMimeType;
        }
        else
        {
            switch (sourceFormat)
            {
                case SKEncodedImageFormat.Jpeg:
                    encoded = pixmap.Encode(new SKJpegEncoderOptions(Options.Quality));
                    mimeType = JpegMimeType;
                    break;
                case SKEncodedImageFormat.Png:
                    encoded = pixmap.Encode(SKPngEncoderOptions.Default);
                    mimeType = PngMimeType;
                    break;
                default:
                    encoded = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Options.Quality));
                    mimeType = WebpMimeType;
                    break;
            }
        }

        using (encoded)
        {
            if (encoded == null)
            {
                throw new InvalidOperationException("The image could not be encoded as " + mimeType + ".");
            }

            return (encoded.ToArray(), mimeType);
        }
    }

    /// <summary>
    /// <paramref name="source"/> scaled to cover the box and cropped to it around the centre - ABP's
    /// <c>ImageResizeMode.Crop</c>. A missing side is the other scaled in proportion, so the picture is only scaled.
    /// </summary>
    protected virtual SKBitmap Resize(SKBitmap source, int targetWidth, int targetHeight)
    {
        if (targetWidth <= 0)
        {
            targetWidth = Math.Max(1, (int)Math.Round((double)source.Width * targetHeight / source.Height));
        }
        else if (targetHeight <= 0)
        {
            targetHeight = Math.Max(1, (int)Math.Round((double)source.Height * targetWidth / source.Width));
        }

        var scale = Math.Max((double)targetWidth / source.Width, (double)targetHeight / source.Height);
        var scaledWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
        var scaled = source.Resize(source.Info.WithSize(scaledWidth, scaledHeight), Options.SKSamplingOptions)
                     ?? throw new InvalidOperationException("The picture could not be resized.");
        if (scaledWidth == targetWidth && scaledHeight == targetHeight)
        {
            return scaled;
        }

        using (scaled)
        {
            var cropped = new SKBitmap(source.Info.WithSize(targetWidth, targetHeight));
            try
            {
                using var canvas = new SKCanvas(cropped);
                var left = (scaledWidth - targetWidth) / 2;
                var top = (scaledHeight - targetHeight) / 2;
                canvas.DrawBitmap(
                    scaled,
                    new SKRect(left, top, left + targetWidth, top + targetHeight),
                    new SKRect(0, 0, targetWidth, targetHeight));
                return cropped;
            }
            catch
            {
                cropped.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// <paramref name="source"/> turned as its EXIF <paramref name="origin"/> says: the bitmap itself when it is
    /// upright already, else a new one.
    /// </summary>
    protected virtual SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return source;
        }

        float w = source.Width;
        float h = source.Height;

        // x' = ScaleX * x + SkewX * y + TransX, y' = SkewY * x + ScaleY * y + TransY: where a stored pixel lands.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        if (matrix.IsIdentity)
        {
            return source;
        }

        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var oriented = new SKBitmap(swapsSides
            ? source.Info.WithSize(source.Height, source.Width)
            : source.Info);
        try
        {
            using var canvas = new SKCanvas(oriented);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(source, 0, 0);
            return oriented;
        }
        catch
        {
            oriented.Dispose();
            throw;
        }
    }
}

/// <summary>An image rendered from a stored picture: its encoded bytes and their MIME type.</summary>
public record ImageVariant(byte[] Content, string MimeType);
