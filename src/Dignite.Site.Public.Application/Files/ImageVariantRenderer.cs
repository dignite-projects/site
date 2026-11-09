using System;
using Dignite.Site.Common.Files;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Files;

/// <summary>
/// Carries out an <see cref="ImageRendition"/> on a stored picture: turned upright, resized when the rendition has a
/// size, and encoded as it says - in one decode and one encode, so a served image is never a re-encode of a
/// re-encode. It decides nothing about what to render; <c>FilePublicAppService</c> does.
/// <para>
/// <b>Formats.</b> JPEG, PNG and WebP are decoded. <see cref="ImageRenditionEncoding.SourceFormat"/> encodes a picture
/// in its own format (a WebP lossy); the WebP encodings use <see cref="SiteImageRenderingOptions.WebpQuality"/> and
/// <see cref="SiteImageRenderingOptions.LosslessWebpEffort"/>.
/// </para>
/// <para>
/// <b>Upright.</b> A camera writes a photo sideways and records how to turn it in its EXIF orientation, which a
/// browser applies to the stored file. Re-encoding drops the EXIF, so the orientation is applied to the pixels here
/// (<see cref="ImageOrientation"/>, which the upload resize uses too).
/// </para>
/// <para>
/// A size is applied as ABP's <c>ImageResizeMode.Crop</c> does, so an address resized before renders the same
/// box: one side given, scaled to it with the other in proportion; both given, scaled to cover the box and
/// cropped to it around the centre. The sampling is <see cref="SiteImageRenderingOptions.Sampling"/>.
/// </para>
/// </summary>
public class ImageVariantRenderer : ITransientDependency
{
    public const string JpegMimeType = "image/jpeg";
    public const string PngMimeType = "image/png";
    public const string WebpMimeType = "image/webp";

    protected SiteImageRenderingOptions Options { get; }

    public ImageVariantRenderer(IOptions<SiteImageRenderingOptions> options)
    {
        Options = options.Value;
    }

    /// <summary>
    /// Renders <paramref name="rendition"/> from the picture in <paramref name="source"/>, which the caller has bounded
    /// by its header.
    /// </summary>
    /// <exception cref="NotSupportedException">The content is not a JPEG, PNG or WebP this can decode.</exception>
    /// <exception cref="InvalidOperationException">The picture's pixels cannot be decoded, or the image not encoded.</exception>
    public virtual ImageVariant Render(byte[] source, ImageRendition rendition)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data) ?? throw new NotSupportedException("Not a picture SkiaSharp can read.");

        var sourceFormat = codec.EncodedFormat;
        if (sourceFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            throw new NotSupportedException("Rendering " + sourceFormat + " is not supported.");
        }

        // 32-bit pixels whatever the source (a 16-bit or greyscale PNG), which every encoder takes; premultiplied
        // unless opaque, which a canvas draws.
        var info = codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        using var decoded = SKBitmap.Decode(codec, info) ?? throw new InvalidOperationException("The picture's pixels could not be decoded.");
        var upright = ImageOrientation.Orient(decoded, codec.EncodedOrigin);
        try
        {
            var resized = rendition.Resizes ? Resize(upright, rendition.Width ?? 0, rendition.Height ?? 0) : upright;
            try
            {
                return Encode(resized, sourceFormat, rendition.Encoding);
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

    protected virtual ImageVariant Encode(SKBitmap bitmap, SKEncodedImageFormat sourceFormat, ImageRenditionEncoding encoding)
    {
        using var pixmap = bitmap.PeekPixels() ?? throw new InvalidOperationException("The image's pixels are not readable.");

        var (encoded, mimeType) = encoding switch
        {
            ImageRenditionEncoding.LossyWebp =>
                (pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Options.WebpQuality)), WebpMimeType),
            ImageRenditionEncoding.LosslessWebp =>
                (pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, Options.LosslessWebpEffort)), WebpMimeType),
            _ => sourceFormat switch
            {
                SKEncodedImageFormat.Jpeg => (pixmap.Encode(new SKJpegEncoderOptions(Options.JpegQuality)), JpegMimeType),
                SKEncodedImageFormat.Png => (pixmap.Encode(SKPngEncoderOptions.Default), PngMimeType),
                _ => (pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Options.WebpQuality)), WebpMimeType)
            }
        };

        using (encoded)
        {
            if (encoded == null)
            {
                throw new InvalidOperationException("The image could not be encoded as " + mimeType + ".");
            }

            return new ImageVariant(encoded.ToArray(), mimeType);
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
        var scaled = source.Resize(source.Info.WithSize(scaledWidth, scaledHeight), Options.Sampling)
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
}
