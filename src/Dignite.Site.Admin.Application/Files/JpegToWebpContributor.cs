using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Site.Common.Files;
using Dignite.Site.Files;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Stores a JPEG as lossy WebP, at <see cref="SiteImageStorageConfiguration.WebpQuality"/>: turned upright by its EXIF
/// orientation and shrunk to the container's <c>ImageResize</c> box in the same decode, so it is encoded once. Every
/// other picture passes through, to the contributors after it. Add it with
/// <see cref="JpegToWebpBlobContainerConfigurationExtensions.StoreJpegAsWebp"/>, before <c>AddImageResizeContributor</c>.
/// <para>
/// Every JPEG is converted, whatever the size of the WebP: <see cref="FileDescriptorManager"/> names the blob <c>.webp</c>
/// and records <c>image/webp</c> before the save, from the same configuration. It derives from Dignite.Abp.BlobStoring's
/// <see cref="ImageContributorBase"/>, so the container's decode guard bounds the picture before anything decodes it.
/// </para>
/// </summary>
public class JpegToWebpContributor : ImageContributorBase, ITransientDependency
{
    /// <summary>The sampling of the shrink: linear between linear-blended mipmap levels, as the resize contributor's.</summary>
    protected virtual SKSamplingOptions Sampling => new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public JpegToWebpContributor(IMimeTypeDetector mimeTypeDetector)
        : base(mimeTypeDetector)
    {
    }

    protected override void ValidateConfiguration(BlobContainerConfiguration configuration)
    {
        configuration.GetSiteImageStorageConfiguration().Validate();
    }

    protected override Task<ImageProcessResult<Stream>?> ProcessAsync(
        BlobPipelineContext context,
        Stream stream,
        string mimeType,
        (int Width, int Height)? dimensions,
        CancellationToken cancellationToken)
    {
        var storage = context.Configuration.GetSiteImageStorageConfiguration();
        if (!storage.StoreJpegAsWebp || mimeType != "image/jpeg")
        {
            return Task.FromResult<ImageProcessResult<Stream>?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] source;
        if (stream is MemoryStream memory)
        {
            source = memory.ToArray();
        }
        else
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            source = copy.ToArray();
        }

        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a JPEG SkiaSharp can read.");
        var info = codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(SKAlphaType.Opaque);
        using var decoded = SKBitmap.Decode(codec, info) ?? throw new InvalidOperationException("The JPEG's pixels could not be decoded.");
        var upright = ImageOrientation.Orient(decoded, codec.EncodedOrigin);
        try
        {
            var box = context.Configuration.GetImageResizeContributorConfiguration();
            var fitted = Fit(upright, box.MaxWidth, box.MaxHeight);
            try
            {
                using var pixmap = fitted.PeekPixels() ?? throw new InvalidOperationException("The picture's pixels are not readable.");
                using var encoded = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, storage.WebpQuality))
                                    ?? throw new InvalidOperationException("The picture could not be encoded as WebP.");
                var result = new MemoryStream(encoded.ToArray(), writable: false);
                return Task.FromResult<ImageProcessResult<Stream>?>(new ImageCompressResult<Stream>(result, ImageProcessState.Done));
            }
            finally
            {
                if (!ReferenceEquals(fitted, upright))
                {
                    fitted.Dispose();
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

    /// <summary>
    /// <paramref name="picture"/> shrunk to fit inside <paramref name="maxWidth"/> x <paramref name="maxHeight"/> (a side
    /// of <c>0</c> is unconstrained), keeping its aspect ratio, as ABP's <c>ImageResizeMode.Max</c>; never enlarged.
    /// </summary>
    protected virtual SKBitmap Fit(SKBitmap picture, int maxWidth, int maxHeight)
    {
        var scale = 1.0;
        if (maxWidth > 0 && picture.Width > maxWidth)
        {
            scale = Math.Min(scale, (double)maxWidth / picture.Width);
        }

        if (maxHeight > 0 && picture.Height > maxHeight)
        {
            scale = Math.Min(scale, (double)maxHeight / picture.Height);
        }

        if (scale >= 1.0)
        {
            return picture;
        }

        var width = Math.Max(1, (int)Math.Round(picture.Width * scale));
        var height = Math.Max(1, (int)Math.Round(picture.Height * scale));
        return picture.Resize(picture.Info.WithSize(width, height), Sampling)
               ?? throw new InvalidOperationException("The picture could not be resized.");
    }
}

public static class JpegToWebpBlobContainerConfigurationExtensions
{
    /// <summary>
    /// Stores every JPEG saved to the container as lossy WebP (<see cref="JpegToWebpContributor"/>), and tells
    /// <see cref="FileDescriptorManager"/> so (<see cref="SiteImageStorageConfiguration.StoreJpegAsWebp"/>). Add it after
    /// <c>AddAllowedContentTypesContributor</c> and before <c>AddImageResizeContributor</c>, whose box it applies.
    /// </summary>
    public static BlobContainerConfiguration StoreJpegAsWebp(this BlobContainerConfiguration containerConfiguration, int quality = 85)
    {
        var storage = containerConfiguration.GetSiteImageStorageConfiguration();
        storage.StoreJpegAsWebp = true;
        storage.WebpQuality = quality;
        storage.Validate();

        containerConfiguration.PipelineContributors.TryAdd<JpegToWebpContributor>();
        return containerConfiguration;
    }
}
