using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Site.Admin.Files;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Themes;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Imaging;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.RemoteServices;
using Volo.Abp.Application;
using Dignite.Site.Common;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(SiteAdminApplicationContractsModule),
    typeof(SiteCommonApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    // The ImageResize contributor (and its localized error codes) - AddImageResizeContributor below only
    // records the contributor type on the container; it is resolved from DI on every save.
    typeof(AbpBlobStoringImagingModule),
    // The imaging provider that contributor hands images to. Without one it finds nothing that can
    // process an image and stores every upload unresized.
    typeof(AbpImagingSkiaSharpModule),
    // The lock the upload links are recorded as used under: one implementation per process by default, a
    // distributed one when the host configures it.
    typeof(AbpDistributedLockingAbstractionsModule),
    // RemoteServices:SitePublicWeb, the site's application, which renders the previews of an administration API
    // deployed apart from it (SiteThemeRemotePreviewRenderer).
    typeof(AbpRemoteServicesModule)
    )]
public class SiteAdminApplicationModule : AbpModule
{
    /// <summary>Largest upload to <see cref="SiteFileContainerNames.Attachments"/>: 20 MB.</summary>
    private const long AttachmentsContainerMaxSizeInBytes = 20 * 1024 * 1024;

    /// <summary>
    /// Largest upload to <see cref="SiteFileContainerNames.Images"/>: 25 MB, as sent - before
    /// <see cref="ImageMetadataStripContributor"/> drops what a design tool piled into it, which is most of an
    /// oversized export (an 18 MB banner kept 0.95 MB). Below the 28.6 MB a server takes in one request by default.
    /// </summary>
    private const long ImagesContainerMaxSizeInBytes = 25 * 1024 * 1024;

    /// <summary>
    /// The box a picture uploaded to <see cref="SiteFileContainerNames.Images"/> is shrunk to fit: 4096 x 4096, the
    /// largest source <c>FilePublicAppService</c> renders.
    /// </summary>
    private const int ImagesContainerMaxDimension = 4096;

    /// <summary>Largest upload to <see cref="SiteFileContainerNames.ThemeAssets"/>: 5 MB, more than any icon or font a page loads.</summary>
    private const long ThemeAssetsContainerMaxSizeInBytes = 5 * 1024 * 1024;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SiteAdminApplicationModule>();

        // Resource-based authorization of the file library: who may read, upload, change or delete a file or
        // a directory, per container. Singletons, as ASP.NET Core registers handlers; they hold no request
        // state (IPermissionChecker and the configuration provider are resolved through singletons too).
        context.Services.AddSingleton<IAuthorizationHandler, FileDescriptorAuthorizationHandler>();
        context.Services.AddSingleton<IAuthorizationHandler, DirectoryDescriptorAuthorizationHandler>();

        // The calls of SiteThemeRemotePreviewRenderer. A render has its own time limit on the site; this one only
        // ends a call to a site that does not answer.
        context.Services.AddHttpClient(SiteThemeRemotePreviewRenderer.HttpClientName, client =>
        {
            client.Timeout = System.TimeSpan.FromMinutes(1);
        });

        ConfigureFileContainers();
        ConfigureImaging();
    }

    /// <summary>
    /// Overrides ABP's SkiaSharp provider defaults, which the upload resize runs on.
    /// <see cref="SkiaSharpResizerOptions"/> defaults to <see cref="SKSamplingOptions.Default"/>, i.e.
    /// nearest-neighbour with no mipmaps: shrinking keeps one source pixel per target pixel and drops the
    /// rest, so fine detail turns to jagged noise and moire. Linear filtering between linear-blended mipmap
    /// levels averages instead. Measured against an exact area-average downscale it scored 25-46 dB PSNR
    /// where the default scored 9-33 dB (zone plate, noisy photo-like image and a UI screenshot, shrunk to
    /// 1920/800/400 px); bicubic (Mitchell, Catmull-Rom) samples a fixed 4x4 neighbourhood and was barely
    /// better than nearest past a 2x reduction. Quality 85 rather than ABP's 75 for the JPEG/WebP
    /// re-encode of a picture shrunk to fit. <c>SitePublicApplicationModule</c> sets the same values for
    /// the images rendered on read, as either module may be hosted without the other.
    /// </summary>
    private void ConfigureImaging()
    {
        Configure<SkiaSharpResizerOptions>(options =>
        {
            options.SKSamplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            options.Quality = 85;
        });
    }

    /// <summary>
    /// The policy half of each <see cref="SiteFileContainerNames"/> container - allowed types, size limit,
    /// who may write. Lives in the module rather than the host so every host (Site's own dev Host, the test
    /// module, Dignite.Cloud) gets the same policy instead of a hand-synced copy; a host adds only the
    /// provider (<c>UseFileSystem</c>/<c>UseDatabase</c>/...) under the same name, which
    /// <c>Containers.Configure</c> merges onto this configuration.
    /// <para>
    /// The upload rules are ABP BLOB pipeline contributors (<c>Dignite.Abp.BlobStoring.Pipeline</c> /
    /// <c>.Imaging</c>), which run inside <c>IBlobContainer.SaveAsync</c> in the order added, in the order the
    /// package prescribes: <c>MaxSize</c> (bytes; rejects a larger upload with
    /// <c>Dignite.Abp.BlobStoring:ContentTooLarge</c>), <c>AllowedContentTypes</c> (the MIME types of
    /// <see cref="SiteFileTypes"/>, matched against the type detected from the bytes, never the uploader's
    /// claim - <c>Dignite.Abp.BlobStoring:ContentTypeNotAllowed</c>), then Site's own
    /// <see cref="ImageMetadataStripContributor"/> (drops the metadata a design tool piles into a JPEG or PNG, keeping
    /// every byte of the image), then <c>ImageResize</c> (shrinks a picture
    /// that exceeds the box, keeping its aspect ratio; a picture that fits is stored as uploaded, and there is
    /// no minimum size). No GZip: the containers' stored format stays the plain file. Only <see cref="SiteFileContainerNames.Images"/>
    /// gets the image contributors: <see cref="SiteFileContainerNames.Attachments"/> takes documents only, so no picture
    /// can enter it. Before the resize contributor decodes anything, the
    /// Imaging package's decode guard (left at its defaults: 8192 x 8192, 50,000,000 pixels, 100 pixels per
    /// stored byte, 10 s) rejects an oversized picture with <c>Dignite.Abp.BlobStoring.Imaging:ImageTooLarge</c>.
    /// </para>
    /// <para>
    /// <b>The image is kept.</b> No <c>ImageCompress</c>: a lossy re-encode on upload cannot be undone, and
    /// every image the site shows is rendered from what is stored (<c>FilePublicAppService</c>: resized, and WebP
    /// for a browser that takes it), so a picture stored already re-encoded loses quality twice. The box is
    /// <see cref="ImagesContainerMaxDimension"/>, the largest source the read side renders, so a picture that fits
    /// the box - a 12-megapixel phone photo among them - keeps its image exactly as uploaded, less only the
    /// metadata <see cref="ImageMetadataStripContributor"/> drops, and a high-density screen still gets a sharp
    /// full-width image.
    /// </para>
    /// <para>
    /// SVG, HTML and script types are deliberately absent from every list - <see cref="SiteFileTypes"/> lists
    /// exact raster types, not <c>image/*</c>, which would admit <c>image/svg+xml</c>: reads are public (see
    /// <see cref="ConfigureContentPermissions"/>), so an uploaded file that a browser renders and executes
    /// would be stored XSS on the site's own origin.
    /// </para>
    /// </summary>
    private void ConfigureFileContainers()
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(SiteFileContainerNames.Attachments, container =>
            {
                container.AddMaxSizeContributor(config => config.MaxSizeInBytes = AttachmentsContainerMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(config =>
                {
                    config.AllowedContentTypes = SiteFileTypes.GetMimeTypes(SiteFileTypes.Documents);
                });
                ConfigureContentPermissions(container);
            });

            // Binary files a theme links - a favicon, later fonts. No image contributors: the icon is stored as it is,
            // an .ico being neither resized nor re-encoded. Written to by the theme's permission, not the content's.
            options.Containers.Configure(SiteFileContainerNames.ThemeAssets, container =>
            {
                container.AddMaxSizeContributor(config => config.MaxSizeInBytes = ThemeAssetsContainerMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(config =>
                {
                    config.AllowedContentTypes = SiteFileTypes.GetMimeTypes(SiteFileTypes.ThemeAssets);
                });
                ConfigurePermissions(container, ContainerPermissions.All(SiteAdminPermissions.Themes.Edit));
            });

            options.Containers.Configure(SiteFileContainerNames.Images, container =>
            {
                container.AddMaxSizeContributor(config => config.MaxSizeInBytes = ImagesContainerMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(config =>
                {
                    config.AllowedContentTypes = SiteFileTypes.GetMimeTypes(SiteFileTypes.Images);
                });
                container.PipelineContributors.TryAdd<ImageMetadataStripContributor>();
                container.AddImageResizeContributor(config =>
                {
                    config.MaxWidth = ImagesContainerMaxDimension;
                    config.MaxHeight = ImagesContainerMaxDimension;
                });
                ConfigureContentPermissions(container);
            });
        });
    }

    /// <summary>
    /// Files ride on the Contents permissions rather than a Files permission group of their own - an upload
    /// is how a content's images and attachments get here, so whoever may create/edit/delete content may do
    /// the same to its files (总体设计 §6.2.5); and <c>SiteAdmin.Contents</c> itself lets a user list and manage
    /// every editor's files (<see cref="FileDescriptorAuthorizationHandler"/>). Update and Delete have to be
    /// set explicitly: the default for unset is "only the file's creator", which would stop one editor from
    /// replacing or removing a file another editor uploaded. CreateDirectory has to be set for the opposite
    /// reason - its default for unset is "nobody" - and goes with Create, since a directory only exists to
    /// hold uploads.
    /// <para>
    /// <c>GetFilePermissionName</c> is deliberately left unset: a published content's files are served to
    /// anonymous visitors by the public read endpoint (<c>api/site-public/files/</c>) regardless, so gating
    /// the admin API's read of the same descriptor would protect nothing.
    /// </para>
    /// </summary>
    private static void ConfigureContentPermissions(BlobContainerConfiguration container)
    {
        ConfigurePermissions(
            container,
            new ContainerPermissions(
                Create: SiteAdminPermissions.Contents.Create,
                Update: SiteAdminPermissions.Contents.Update,
                Delete: SiteAdminPermissions.Contents.Delete,
                CreateDirectory: SiteAdminPermissions.Contents.Create));
    }

    /// <summary>The permission each write to a container needs.</summary>
    private sealed record ContainerPermissions(string Create, string Update, string Delete, string CreateDirectory)
    {
        /// <summary>The same permission for every write.</summary>
        public static ContainerPermissions All(string permission) => new(permission, permission, permission, permission);
    }

    private static void ConfigurePermissions(BlobContainerConfiguration container, ContainerPermissions permissions)
    {
        container.SetAuthorizationConfiguration(config =>
        {
            config.CreateFilePermissionName = permissions.Create;
            config.UpdateFilePermissionName = permissions.Update;
            config.DeleteFilePermissionName = permissions.Delete;
            config.CreateDirectoryPermissionName = permissions.CreateDirectory;
        });
    }
}
