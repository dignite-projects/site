using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Site.Files;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Threading;

namespace Dignite.Site.Public.Files;

/// <summary>
/// The public read endpoint's logic: find the live descriptor in the current tenant (the request's, resolved
/// like any other), open its bytes, and render an image on the way out - resized when a size is asked, WebP when
/// the caller takes it (<see cref="ImageVariantRenderer"/>).
/// <para>
/// Anonymous on purpose, and not feature-gated (see <c>SiteFeatures.Enable</c>): it serves a published
/// site's images to its visitors. What keeps it from serving anything else is the container allow-list -
/// <see cref="FileDescriptorManager.FindAsync"/> answers only for <see cref="SiteFileContainerNames.All"/> -
/// and the containers' own upload rules, which keep script-capable types (SVG, HTML, JS) out of them.
/// </para>
/// </summary>
public class FilePublicAppService : SitePublicAppService, IFilePublicAppService
{
    protected const int MaxResizeDimension = 4096;
    protected const long MaxResizePixelCount = 4096L * 4096;
    protected const int MaxDecompressionRatio = 100;
    protected const int MaxCachedImageBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Rendered images, in a cache owned by this service: the host's shared <see cref="IMemoryCache"/> may
    /// hold unrelated entries and must not decide this endpoint's eviction. Keyed by the actual blob, which
    /// is immutable, so an entry never goes stale - it only ages out.
    /// </summary>
    private static readonly IMemoryCache RenderedImageCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 64 * 1024 * 1024
    });

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected ImageVariantRenderer ImageVariantRenderer { get; }

    protected CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    public FilePublicAppService(FileDescriptorManager fileDescriptorManager, ImageVariantRenderer imageVariantRenderer)
    {
        FileDescriptorManager = fileDescriptorManager;
        ImageVariantRenderer = imageVariantRenderer;
    }

    public virtual async Task<IRemoteStreamContent> GetAsync(string containerName, string blobName, GetFileInput input)
    {
        var cancellationToken = RequestCancellationToken;

        var file = await FileDescriptorManager.FindAsync(containerName, blobName, cancellationToken);
        if (file == null)
        {
            throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");
        }

        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file, cancellationToken);
        if (stream == null)
        {
            throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");
        }

        var resize = (input.Width ?? 0) > 0 || (input.Height ?? 0) > 0;
        if (!resize && !(input.AcceptWebp && ImageVariantRenderer.ConvertsToWebp(file.MimeType)))
        {
            return Stored(file, stream);
        }

        return await RenderAsync(file, stream, input, resize, cancellationToken);
    }

    /// <summary>
    /// Renders an image within bounds that keep one request from costing unbounded CPU or memory: the target
    /// dimensions, and the source dimensions, pixel count and pixels per stored byte (read from the image
    /// header, without decoding), are checked before anything is decoded. SkiaSharp decodes synchronously and
    /// cannot be interrupted, so these checks are the protection.
    /// <para>
    /// Whether a file is an image is decided by its recorded <see cref="FileDescriptor.MimeType"/>, the type
    /// detected from its bytes on upload. Anything that is not one of Site's raster image types, a picture whose
    /// header cannot be read, and a picture the renderer leaves alone (a GIF, or a WebP no smaller than the stored
    /// file) are served as stored. With a size asked, limits fail with
    /// <see cref="BlobStoringImagingErrorCodes.ImageTooLarge"/> and a picture that cannot be decoded with
    /// <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/> - the codes the upload contributors use.
    /// Without one the request only asked for WebP, which a picture beyond the limits is served without.
    /// </para>
    /// </summary>
    protected virtual async Task<IRemoteStreamContent> RenderAsync(
        FileDescriptor file,
        Stream stream,
        GetFileInput input,
        bool resize,
        CancellationToken cancellationToken)
    {
        var disposeStream = true;
        try
        {
            if ((input.Width ?? 0) > MaxResizeDimension || (input.Height ?? 0) > MaxResizeDimension)
            {
                throw CreateImageTooLargeException(
                    "Image resize dimensions cannot exceed " + MaxResizeDimension + " pixels.",
                    input.Width,
                    input.Height);
            }

            if (!SiteFileTypes.IsImage(file.MimeType))
            {
                // Not an image: serve the original. The response now owns the stream.
                disposeStream = false;
                return Stored(file, stream);
            }

            if (!stream.CanSeek)
            {
                var buffered = new MemoryStream();
                await stream.CopyToAsync(buffered, cancellationToken);
                await stream.DisposeAsync();
                buffered.Position = 0;
                stream = buffered;
            }

            var dimensions = ImageDimensionReader.Read(stream);
            if (dimensions == null)
            {
                // A header that cannot be read cannot be bounded either: rather than decode an unbounded picture,
                // serve it as stored.
                stream.Position = 0;
                disposeStream = false;
                return Stored(file, stream);
            }

            var (width, height) = dimensions.Value;
            var totalPixels = (long)width * height;
            if (width > MaxResizeDimension ||
                height > MaxResizeDimension ||
                totalPixels > MaxResizePixelCount ||
                stream.Length > 0 && totalPixels / (double)stream.Length > MaxDecompressionRatio)
            {
                if (!resize)
                {
                    stream.Position = 0;
                    disposeStream = false;
                    return Stored(file, stream);
                }

                throw CreateImageTooLargeException("The image is too large to resize safely.", width, height);
            }

            var cacheKey = $"{file.TenantId}:{file.ContainerName}:{file.GetActualBlobName()}:{input.Width}:{input.Height}:{(input.AcceptWebp ? "webp" : "")}";
            if (!RenderedImageCache.TryGetValue<RenderedImage>(cacheKey, out var rendered) || rendered == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rendered = Render(stream, input);
                if (rendered.Cacheable)
                {
                    // An entry that says "serve the stored picture" is cached too, so a PNG whose WebP is no
                    // smaller is not encoded again on every request.
                    RenderedImageCache.Set(cacheKey, rendered, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        Size = Math.Max(1, rendered.Variant?.Content.LongLength ?? 0)
                    });
                }
            }

            if (rendered.Variant == null)
            {
                // Nothing to render (a GIF), or a WebP no smaller than the stored picture: serve the original.
                stream.Position = 0;
                disposeStream = false;
                return Stored(file, stream);
            }

            var content = rendered.Variant.Content;
            return new RemoteStreamContent(
                new MemoryStream(content, writable: false),
                FileNameFor(file.Name, rendered.Variant.MimeType),
                rendered.Variant.MimeType,
                content.LongLength,
                disposeStream: true);
        }
        finally
        {
            if (disposeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Renders <paramref name="stream"/> (seekable, an image of a bounded size). An image beyond
    /// <see cref="MaxCachedImageBytes"/> is served but not cached.
    /// </summary>
    protected virtual RenderedImage Render(Stream stream, GetFileInput input)
    {
        stream.Position = 0;
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

        ImageVariant? variant;
        try
        {
            variant = ImageVariantRenderer.Render(source, input.Width, input.Height, input.AcceptWebp);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException or BusinessException))
        {
            // Corrupt pixel data or an encoder failure: this image failing, not the server.
            throw CreateProcessingFailedException(exception);
        }

        if (variant != null && variant.Content.LongLength > MaxCachedImageBytes)
        {
            // Too large to keep, so it is rendered again on the next request.
            return new RenderedImage(variant, Cacheable: false);
        }

        return new RenderedImage(variant);
    }

    /// <summary>
    /// The file name a rendered image is served under: the stored name with the extension of the rendered type,
    /// so a picture a visitor saves is named for what it is.
    /// </summary>
    protected virtual string FileNameFor(string fileName, string mimeType)
    {
        return mimeType == ImageVariantRenderer.WebpMimeType &&
               !fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            ? Path.ChangeExtension(fileName, ".webp")
            : fileName;
    }

    protected virtual IRemoteStreamContent Stored(FileDescriptor file, Stream stream)
    {
        return new RemoteStreamContent(stream, file.Name, file.MimeType, stream.CanSeek ? stream.Length : null, disposeStream: true);
    }

    protected virtual BusinessException CreateImageTooLargeException(string message, int? width = null, int? height = null)
    {
        var exception = new BusinessException(code: BlobStoringImagingErrorCodes.ImageTooLarge, message: message)
            .WithData("MaxWidth", MaxResizeDimension)
            .WithData("MaxHeight", MaxResizeDimension)
            .WithData("MaxPixels", MaxResizePixelCount)
            .WithData("MaxDecompressionRatio", MaxDecompressionRatio);

        if (width.HasValue)
        {
            exception.WithData("Width", width.Value);
        }

        if (height.HasValue)
        {
            exception.WithData("Height", height.Value);
        }

        return exception;
    }

    protected virtual BusinessException CreateProcessingFailedException(Exception? innerException)
    {
        return new BusinessException(
            code: BlobStoringImagingErrorCodes.ImageProcessingFailed,
            message: "The image could not be resized.",
            details: innerException?.Message,
            innerException: innerException);
    }

    /// <summary>
    /// What <see cref="ImageVariantRenderer"/> made of a picture: the image to serve, or <c>null</c> to serve the
    /// stored picture.
    /// </summary>
    protected record RenderedImage(ImageVariant? Variant, bool Cacheable = true);
}
