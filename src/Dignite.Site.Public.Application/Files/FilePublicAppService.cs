using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.Abp.FileStoring.Imaging;
using Dignite.Site.Files;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
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
    /// Resizes an image within bounds that keep one request from costing unbounded CPU or memory: target
    /// and source dimensions, pixel count and compression ratio are checked, and the decode is time-boxed,
    /// before any full decode. A file that is not an image is returned as is.
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
                throw new BusinessException(
                    code: FileStoringImagingErrorCodes.ImageResizeDimensionsTooLarge,
                    message: $"Image resize dimensions cannot exceed {MaxResizeDimension} pixels.");
            }

            if (!stream.CanSeek)
            {
                var buffered = new MemoryStream();
                await stream.CopyToAsync(buffered, cancellationToken);
                await stream.DisposeAsync();
                buffered.Position = 0;
                stream = buffered;
            }

            var imageInfo = await IdentifyImageAsync(stream);
            var format = imageInfo?.Metadata.DecodedImageFormat;
            if (imageInfo == null || format == null ||
                !ImageFormatHelper.IsValidImage(format.DefaultMimeType, ImageFormatHelper.AllowedImageUploadFormats))
            {
                // Not a resizable image: serve the original. The response now owns the stream.
                disposeStream = false;
                return new RemoteStreamContent(stream, file.Name, file.MimeType, stream.Length, disposeStream: true);
            }

            var totalPixels = (long)imageInfo.Width * imageInfo.Height;
            if (imageInfo.Width > MaxResizeDimension ||
                imageInfo.Height > MaxResizeDimension ||
                totalPixels > MaxResizePixelCount ||
                stream.Length > 0 && totalPixels / (double)stream.Length > MaxDecompressionRatio)
            {
                throw new BusinessException(
                    code: FileStoringImagingErrorCodes.ImageTooLarge,
                    message: "The image is too large to resize safely.");
            }

            var cacheKey = $"{file.TenantId}:{file.ContainerName}:{file.GetActualBlobName()}:{input.Width}:{input.Height}";
            if (ResizedImageCache.TryGetValue<byte[]>(cacheKey, out var cached) && cached != null)
            {
                return new RemoteStreamContent(new MemoryStream(cached, writable: false), file.Name, format.DefaultMimeType, cached.LongLength, disposeStream: true);
            }

            stream.Position = 0;
            var result = await ImageResizer.ResizeAsync(
                stream,
                new ImageResizeArgs(
                    input.Width > 0 ? (uint)input.Width : null,
                    input.Height > 0 ? (uint)input.Height : null,
                    ImageResizeMode.Crop),
                format.DefaultMimeType,
                cancellationToken);

            if (result.Result == null || !(result.State == ImageProcessState.Done || result.Result.CanRead))
            {
                result.Result?.Dispose();
                throw new BusinessException(
                    code: FileStoringImagingErrorCodes.ImageResizeFailure,
                    message: result.State.ToString());
            }

            byte[] resized;
            using (result.Result)
            {
                resized = await ReadBoundedAsync(result.Result, MaxResizePixelCount * 4, cancellationToken);
            }

            if (resized.Length <= MaxCachedImageBytes)
            {
                ResizedImageCache.Set(cacheKey, resized, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    Size = resized.LongLength
                });
            }

            return new RemoteStreamContent(new MemoryStream(resized, writable: false), file.Name, format.DefaultMimeType, resized.LongLength, disposeStream: true);
        }
        finally
        {
            if (disposeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    protected virtual async Task<ImageInfo?> IdentifyImageAsync(Stream stream)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(DecodeTimeoutSeconds));
        try
        {
            stream.Position = 0;
            var info = await Image.IdentifyAsync(stream, timeout.Token);
            stream.Position = 0;
            return info;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new BusinessException(
                code: FileStoringImagingErrorCodes.ImageDecodeTimeout,
                message: "Image decoding timed out.");
        }
        catch (UnknownImageFormatException)
        {
            stream.Position = 0;
            return null;
        }
        catch (ImageFormatException exception)
        {
            throw new BusinessException(
                code: FileStoringImagingErrorCodes.ImageFormatNotSupported,
                message: "The image content is invalid.",
                details: exception.Message);
        }
    }

    protected static async Task<byte[]> ReadBoundedAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
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
                throw new BusinessException(
                    code: FileStoringImagingErrorCodes.ImageTooLarge,
                    message: "The resized image is too large.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }
}
