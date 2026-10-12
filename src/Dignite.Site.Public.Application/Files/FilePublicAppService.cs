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
using Volo.Abp.Imaging;
using Volo.Abp.Threading;

namespace Dignite.Site.Public.Files;

/// <summary>
/// The public read endpoint's logic: find the live descriptor in the current tenant (resolved from the
/// address's <c>__tenant</c>), open its bytes, and optionally resize an image on the way out.
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
    protected const long MaxResizePixelCount = 16_000_000;
    protected const int MaxDecompressionRatio = 100;
    protected const int DecodeTimeoutSeconds = 10;
    protected const int MaxCachedImageBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Resized images, in a cache owned by this service: the host's shared <see cref="IMemoryCache"/> may
    /// hold unrelated entries and must not decide this endpoint's eviction. Keyed by the actual blob, which
    /// is immutable, so an entry never goes stale - it only ages out.
    /// </summary>
    private static readonly IMemoryCache ResizedImageCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 64 * 1024 * 1024
    });

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected IImageResizer ImageResizer { get; }

    protected CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    public FilePublicAppService(FileDescriptorManager fileDescriptorManager, IImageResizer imageResizer)
    {
        FileDescriptorManager = fileDescriptorManager;
        ImageResizer = imageResizer;
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

        if ((input.Width ?? 0) <= 0 && (input.Height ?? 0) <= 0)
        {
            return new RemoteStreamContent(stream, file.Name, file.MimeType, stream.CanSeek ? stream.Length : null, disposeStream: true);
        }

        return await ResizeAsync(file, stream, input, cancellationToken);
    }

    /// <summary>
    /// Resizes an image within bounds that keep one request from costing unbounded CPU or memory: the target
    /// dimensions, and the source dimensions, pixel count and pixels per stored byte (read from the image
    /// header, without decoding), are checked before the imaging provider decodes anything, and the provider
    /// runs under a timeout - which only interrupts a provider that observes its cancellation token (SkiaSharp
    /// decodes synchronously, so for it the header checks are the protection).
    /// <para>
    /// Whether a file is a resizable image is decided by its recorded <see cref="FileDescriptor.MimeType"/>,
    /// the type detected from its bytes on upload. Anything that is not one of Site's raster image types, a
    /// picture whose header cannot be read, and a picture the provider does not handle (SkiaSharp resizes JPEG,
    /// PNG and WebP; a GIF is served as stored) are returned as stored. Limits fail with
    /// <see cref="BlobStoringImagingErrorCodes.ImageTooLarge"/>, a provider that throws or gives up with
    /// <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/>, the timeout with
    /// <see cref="BlobStoringImagingErrorCodes.ImageDecodeTimeout"/> - the codes the upload contributors use.
    /// </para>
    /// </summary>
    protected virtual async Task<IRemoteStreamContent> ResizeAsync(
        FileDescriptor file,
        Stream stream,
        GetFileInput input,
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
                // Not a resizable image: serve the original. The response now owns the stream.
                disposeStream = false;
                return new RemoteStreamContent(stream, file.Name, file.MimeType, stream.CanSeek ? stream.Length : null, disposeStream: true);
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
                // A header that cannot be read cannot be bounded either: rather than hand an unbounded decode to
                // the provider, serve the picture as stored.
                stream.Position = 0;
                disposeStream = false;
                return new RemoteStreamContent(stream, file.Name, file.MimeType, stream.Length, disposeStream: true);
            }

            var (width, height) = dimensions.Value;
            var totalPixels = (long)width * height;
            if (width > MaxResizeDimension ||
                height > MaxResizeDimension ||
                totalPixels > MaxResizePixelCount ||
                stream.Length > 0 && totalPixels / (double)stream.Length > MaxDecompressionRatio)
            {
                throw CreateImageTooLargeException("The image is too large to resize safely.", width, height);
            }

            var cacheKey = $"{file.TenantId}:{file.ContainerName}:{file.GetActualBlobName()}:{input.Width}:{input.Height}";
            if (ResizedImageCache.TryGetValue<byte[]>(cacheKey, out var cached) && cached != null)
            {
                return new RemoteStreamContent(new MemoryStream(cached, writable: false), file.Name, file.MimeType, cached.LongLength, disposeStream: true);
            }

            stream.Position = 0;
            var resizedStream = await ResizeWithProviderAsync(file, stream, input, cancellationToken);
            if (resizedStream == null)
            {
                // The provider does not handle this format: serve the original. The response now owns the stream.
                stream.Position = 0;
                disposeStream = false;
                return new RemoteStreamContent(stream, file.Name, file.MimeType, stream.Length, disposeStream: true);
            }

            byte[] resized;
            using (resizedStream)
            {
                if (resizedStream.CanSeek)
                {
                    resizedStream.Position = 0;
                }

                resized = await ReadBoundedAsync(resizedStream, MaxResizePixelCount * 4, cancellationToken);
            }

            if (resized.Length <= MaxCachedImageBytes)
            {
                ResizedImageCache.Set(cacheKey, resized, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    Size = resized.LongLength
                });
            }

            // The providers re-encode in the source format, so the recorded type still describes the bytes.
            return new RemoteStreamContent(new MemoryStream(resized, writable: false), file.Name, file.MimeType, resized.LongLength, disposeStream: true);
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
    /// Crops <paramref name="stream"/> (seekable, at position 0, an image of the file's type) to the requested
    /// size with ABP's <see cref="IImageResizer"/>. Returns the resized image, or <c>null</c> when the provider
    /// reports the format <see cref="ImageProcessState.Unsupported"/>. Never disposes <paramref name="stream"/>.
    /// </summary>
    protected virtual async Task<Stream?> ResizeWithProviderAsync(
        FileDescriptor file,
        Stream stream,
        GetFileInput input,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(DecodeTimeoutSeconds));

        ImageProcessResult<Stream> result;
        try
        {
            result = await ImageResizer.ResizeAsync(
                stream,
                new ImageResizeArgs(
                    input.Width > 0 ? (uint)input.Width : null,
                    input.Height > 0 ? (uint)input.Height : null,
                    ImageResizeMode.Crop),
                file.MimeType,
                timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw CreateDecodeTimeoutException();
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException or BusinessException))
        {
            // ABP's resizers do not wrap provider errors (corrupt pixel data, an encoder failure); whatever the
            // provider, that is this image failing, not the server.
            throw CreateProcessingFailedException(exception);
        }

        if (result.State == ImageProcessState.Done && result.Result != null)
        {
            return result.Result;
        }

        if (result.Result != null && !ReferenceEquals(result.Result, stream))
        {
            await result.Result.DisposeAsync();
        }

        if (result.State == ImageProcessState.Unsupported)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (timeout.IsCancellationRequested)
        {
            throw CreateDecodeTimeoutException();
        }

        throw CreateProcessingFailedException(null);
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

    protected virtual BusinessException CreateDecodeTimeoutException()
    {
        return new BusinessException(
                code: BlobStoringImagingErrorCodes.ImageDecodeTimeout,
                message: "Image decoding timed out.")
            .WithData("DecodeTimeoutSeconds", DecodeTimeoutSeconds);
    }

    protected virtual BusinessException CreateProcessingFailedException(Exception? innerException)
    {
        return new BusinessException(
            code: BlobStoringImagingErrorCodes.ImageProcessingFailed,
            message: "The image could not be resized.",
            details: innerException?.Message,
            innerException: innerException);
    }

    protected virtual async Task<byte[]> ReadBoundedAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw CreateImageTooLargeException("The resized image is too large.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }
}
