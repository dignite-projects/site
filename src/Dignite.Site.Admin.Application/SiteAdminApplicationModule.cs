using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Site.Admin.Directories;
using Dignite.Site.Admin.Files;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BlobStoring;
using Volo.Abp.Imaging;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Site.Common;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(SiteAdminApplicationContractsModule),
    typeof(SiteCommonApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    // The ImageResize/ImageCompress contributors (and their localized error codes) - AddImageResizeContributor
    // below only records the contributor type on the container; it is resolved from DI on every save.
    typeof(AbpBlobStoringImagingModule),
    // The imaging provider those contributors hand images to. Without one they find nothing that can
    // process an image and store every upload unresized.
    typeof(AbpImagingSkiaSharpModule)
    )]
public class SiteAdminApplicationModule : AbpModule
{
    /// <summary>Largest upload to <see cref="SiteFileContainerNames.Default"/>: 20 MB.</summary>
    private const long DefaultContainerMaxSizeInBytes = 20 * 1024 * 1024;

    /// <summary>Largest upload to <see cref="SiteFileContainerNames.Images"/>: 10 MB.</summary>
    private const long ImagesContainerMaxSizeInBytes = 10 * 1024 * 1024;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SiteAdminApplicationModule>();

        // Resource-based authorization of the file library: who may read, upload, change or delete a file or
        // a directory, per container. Singletons, as ASP.NET Core registers handlers; they hold no request
        // state (IPermissionChecker and the configuration provider are resolved through singletons too).
        context.Services.AddSingleton<IAuthorizationHandler, FileDescriptorAuthorizationHandler>();
        context.Services.AddSingleton<IAuthorizationHandler, DirectoryDescriptorAuthorizationHandler>();

        ConfigureFileContainers();
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
    /// claim - <c>Dignite.Abp.BlobStoring:ContentTypeNotAllowed</c>), <c>ImageResize</c> (shrinks a picture
    /// that exceeds the box, keeping its aspect ratio; a picture that fits is stored as uploaded, and there is
    /// no minimum size), then <c>ImageCompress</c> (re-encodes a picture in its own format, kept only when it
    /// comes out smaller). No GZip: the containers' stored format stays the plain file. Content that is not an
    /// image passes the image contributors untouched. Before either image contributor decodes anything, the
    /// Imaging package's decode guard (left at its defaults: 8192 x 8192, 50,000,000 pixels, 100 pixels per
    /// stored byte, 10 s) rejects an oversized picture with <c>Dignite.Abp.BlobStoring.Imaging:ImageTooLarge</c>.
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
            options.Containers.Configure(SiteFileContainerNames.Default, container =>
            {
                container.AddMaxSizeContributor(config => config.MaxSizeInBytes = DefaultContainerMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(config =>
                {
                    config.AllowedContentTypes = SiteFileTypes.GetMimeTypes(SiteFileTypes.All);
                });
                container.AddImageResizeContributor(config =>
                {
                    config.MaxWidth = 4096;
                    config.MaxHeight = 4096;
                });
                container.AddImageCompressContributor();
                ConfigureContentPermissions(container);
            });

            options.Containers.Configure(SiteFileContainerNames.Images, container =>
            {
                container.AddMaxSizeContributor(config => config.MaxSizeInBytes = ImagesContainerMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(config =>
                {
                    config.AllowedContentTypes = SiteFileTypes.GetMimeTypes(SiteFileTypes.Images);
                });
                container.AddImageResizeContributor(config =>
                {
                    config.MaxWidth = 1920;
                    config.MaxHeight = 1920;
                });
                container.AddImageCompressContributor();
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
        container.SetAuthorizationConfiguration(config =>
        {
            config.CreateFilePermissionName = SiteAdminPermissions.Contents.Create;
            config.UpdateFilePermissionName = SiteAdminPermissions.Contents.Update;
            config.DeleteFilePermissionName = SiteAdminPermissions.Contents.Delete;
            config.CreateDirectoryPermissionName = SiteAdminPermissions.Contents.Create;
        });
    }
}
