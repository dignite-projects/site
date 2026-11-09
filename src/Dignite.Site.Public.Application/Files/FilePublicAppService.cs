using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Site.Files;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Threading;

namespace Dignite.Site.Public.Files;

/// <summary>
/// The public read endpoint's logic: find the live descriptor in the current tenant (the request's, resolved
/// like any other), open its bytes, and serve an image rendered from it when the request asks for a size or a
/// format.
/// <para>
/// <b>What is rendered</b> is decided here, from the request and the file's recorded type, as an
/// <see cref="ImageRendition"/> (<see cref="PlanRendition"/>); <see cref="ImageVariantRenderer"/> carries it out and
/// <see cref="RenderedImageCache"/> bounds the work.
/// </para>
/// <para>
/// Anonymous on purpose, and not feature-gated (see <c>SiteFeatures.Enable</c>): it serves a published
/// site's images to its visitors. What keeps it from serving anything else is the container allow-list -
/// <see cref="FileDescriptorManager.FindAsync"/> answers only for <see cref="SiteFileContainerNames.All"/> -
/// and the containers' own upload rules, which keep script-capable types (SVG, HTML, JS) out of them.
/// </para>
/// </summary>
public class FilePublicAppService : SitePublicAppService, IFilePublicAppService
{
    protected const int MaxDecompressionRatio = 100;

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected ImageVariantRenderer ImageVariantRenderer { get; }

    protected RenderedImageCache RenderedImageCache { get; }

    protected CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    public FilePublicAppService(
        FileDescriptorManager fileDescriptorManager,
        ImageVariantRenderer imageVariantRenderer,
        RenderedImageCache renderedImageCache)
    {
        FileDescriptorManager = fileDescriptorManager;
        ImageVariantRenderer = imageVariantRenderer;
        RenderedImageCache = renderedImageCache;
    }

    public virtual async Task<IRemoteStreamContent> GetAsync(string containerName, string blobName, GetFileInput input)
    {
        var cancellationToken = RequestCancellationToken;

        var file = await FileDescriptorManager.FindAsync(containerName, blobName, cancellationToken);
        if (file == null)
        {
            throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");
        }

        if ((input.Width ?? 0) > SiteImageConsts.MaxDimension || (input.Height ?? 0) > SiteImageConsts.MaxDimension)
        {
            throw CreateImageTooLargeException(
                "Image resize dimensions cannot exceed " + SiteImageConsts.MaxDimension + " pixels.",
                input.Width,
                input.Height);
        }

        var rendition = PlanRendition(file, input);
        if (rendition != null)
        {
            var cacheKey = $"{file.TenantId}:{file.ContainerName}:{file.GetActualBlobName()}:{rendition.CacheKey}";
            var rendered = await RenderedImageCache.GetOrRenderAsync(
                cacheKey,
                token => RenderAsync(file, rendition, token),
                cancellationToken);

            if (rendered.Variant != null)
            {
                var content = rendered.Variant.Content;
                return new RemoteStreamContent(
                    new MemoryStream(content, writable: false),
                    FileNameFor(file.Name, rendered.Variant.MimeType),
                    rendered.Variant.MimeType,
                    content.LongLength,
                    disposeStream: true);
            }
        }

        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file, cancellationToken);
        if (stream == null)
        {
            throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");
        }

        return new RemoteStreamContent(stream, file.Name, file.MimeType, stream.CanSeek ? stream.Length : null, disposeStream: true);
    }

    /// <summary>
    /// What to render for <paramref name="input"/> from <paramref name="file"/>, or <c>null</c> to serve it as stored:
    /// a file that is not a JPEG, PNG or WebP (a GIF, a document), or a request that asks for neither a size nor
    /// WebP. With <see cref="SiteFileFormats.Webp"/> a JPEG goes to lossy WebP and a PNG to lossless WebP: a PNG is
    /// usually a screenshot, a logo or a drawing with text, whose edges lossy WebP blurs, since it stores colour at
    /// half the resolution of brightness. <see cref="SiteFileFormats.Auto"/> is <see cref="SiteFileFormats.Original"/>
    /// here: choosing by the request's <c>Accept</c> is the HTTP endpoint's.
    /// </summary>
    protected virtual ImageRendition? PlanRendition(FileDescriptor file, GetFileInput input)
    {
        var isJpeg = string.Equals(file.MimeType, ImageVariantRenderer.JpegMimeType, StringComparison.OrdinalIgnoreCase);
        var isPng = string.Equals(file.MimeType, ImageVariantRenderer.PngMimeType, StringComparison.OrdinalIgnoreCase);
        var isWebp = string.Equals(file.MimeType, ImageVariantRenderer.WebpMimeType, StringComparison.OrdinalIgnoreCase);
        if (!isJpeg && !isPng && !isWebp)
        {
            return null;
        }

        int? width = input.Width > 0 ? input.Width : null;
        int? height = input.Height > 0 ? input.Height : null;
        var toWebp = SiteFileFormats.Normalize(input.Format) == SiteFileFormats.Webp && (isJpeg || isPng);
        if (width == null && height == null && !toWebp)
        {
            return null;
        }

        var encoding = !toWebp
            ? ImageRenditionEncoding.SourceFormat
            : isPng ? ImageRenditionEncoding.LosslessWebp : ImageRenditionEncoding.LossyWebp;
        return new ImageRendition(width, height, encoding);
    }

    /// <summary>
    /// Renders <paramref name="rendition"/> within bounds that keep one request from costing unbounded CPU or memory:
    /// the source's dimensions, pixel count and pixels per stored byte, read from its header without decoding, are
    /// checked first. SkiaSharp decodes synchronously and cannot be interrupted, so these checks (and the render slots
    /// of <see cref="RenderedImageCache"/>) are the protection.
    /// <para>
    /// The stored picture is served instead (<see cref="RenderedImage.Stored"/>) when its header cannot be read, when
    /// SkiaSharp cannot read it, and when a rendition at the stored size comes out no smaller than the stored file. A
    /// rendition with a size fails beyond the limits with <see cref="BlobStoringImagingErrorCodes.ImageTooLarge"/> and
    /// on a picture that cannot be decoded with <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/> - the
    /// codes the upload contributors use; one without a size only asked for WebP, which such a picture is served without.
    /// </para>
    /// </summary>
    protected virtual async Task<RenderedImage> RenderAsync(FileDescriptor file, ImageRendition rendition, CancellationToken cancellationToken)
    {
        byte[] source;
        await using (var stream = await FileDescriptorManager.GetStreamOrNullAsync(file, cancellationToken))
        {
            if (stream == null)
            {
                return RenderedImage.Stored;
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            source = buffer.ToArray();
        }

        using (var probe = new MemoryStream(source, writable: false))
        {
            var dimensions = ImageHeaderReader.Read(probe);
            if (dimensions == null)
            {
                return RenderedImage.Stored;
            }

            var (width, height) = dimensions.Value;
            var totalPixels = (long)width * height;
            if (width > SiteImageConsts.MaxDimension ||
                height > SiteImageConsts.MaxDimension ||
                totalPixels > SiteImageConsts.MaxPixelCount ||
                source.Length > 0 && totalPixels / (double)source.Length > MaxDecompressionRatio)
            {
                if (!rendition.Resizes)
                {
                    return RenderedImage.Stored;
                }

                throw CreateImageTooLargeException("The image is too large to resize safely.", width, height);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        ImageVariant variant;
        try
        {
            variant = ImageVariantRenderer.Render(source, rendition);
        }
        catch (NotSupportedException)
        {
            return RenderedImage.Stored;
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException or BusinessException))
        {
            // Corrupt pixel data or an encoder failure: this image failing, not the server.
            throw CreateProcessingFailedException(exception);
        }

        return !rendition.Resizes && variant.Content.Length >= source.Length
            ? RenderedImage.Stored
            : new RenderedImage(variant);
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

    protected virtual BusinessException CreateImageTooLargeException(string message, int? width = null, int? height = null)
    {
        var exception = new BusinessException(code: BlobStoringImagingErrorCodes.ImageTooLarge, message: message)
            .WithData("MaxWidth", SiteImageConsts.MaxDimension)
            .WithData("MaxHeight", SiteImageConsts.MaxDimension)
            .WithData("MaxPixels", SiteImageConsts.MaxPixelCount)
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
}
