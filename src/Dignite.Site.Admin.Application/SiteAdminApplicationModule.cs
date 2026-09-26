using Dignite.Abp.FileStoring;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Files;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BlobStoring;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Site.Common;
using Dignite.Abp.FileStoring.Imaging;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(SiteAdminApplicationContractsModule),
    typeof(SiteCommonApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    // Registers ImageResizeHandler (and the ImageSharp resizer/compressor it needs) - AddImageResizeHandler
    // below only records the handler type on the container; without this module every upload to that
    // container fails resolving it, non-image files included.
    typeof(DigniteAbpFileStoringImagingModule)
    )]
public class SiteAdminApplicationModule : AbpModule
{
    private static readonly string[] ImageFileTypes = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    private static readonly string[] DocumentFileTypes =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip"];

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SiteAdminApplicationModule>();

        ConfigureFileContainers();
    }

    /// <summary>
    /// The policy half of each <see cref="SiteFileContainerNames"/> container - allowed types, size limit,
    /// who may write. Lives in the module rather than the host so every host (Site's own dev Host, the test
    /// module, Dignite.Cloud) gets the same policy instead of a hand-synced copy; a host adds only the
    /// provider (<c>UseFileSystem</c>/<c>UseDatabase</c>/...) under the same name, which
    /// <c>Containers.Configure</c> merges onto this configuration.
    /// <para>
    /// SVG, HTML and script types are deliberately absent from every list: reads are public (see
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
                container.AddFileSizeLimitHandler(config => config.MaxFileSize = 20); // MB
                container.AddFileTypeCheckHandler(config =>
                {
                    config.AllowedFileTypeNames = [.. ImageFileTypes, .. DocumentFileTypes];
                });
                container.AddImageResizeHandler(handler =>
                {
                    handler.ImageWidth = 4096;
                    handler.ImageHeight = 4096;
                });
                ConfigureContentPermissions(container);
            });

            options.Containers.Configure(SiteFileContainerNames.Images, container =>
            {
                container.AddFileSizeLimitHandler(config => config.MaxFileSize = 10); // MB
                container.AddFileTypeCheckHandler(config =>
                {
                    config.AllowedFileTypeNames = ImageFileTypes;
                });
                container.AddImageResizeHandler(handler =>
                {
                    handler.ImageWidth = 1920;
                    handler.ImageHeight = 1920;
                });
                ConfigureContentPermissions(container);
            });
        });
    }

    /// <summary>
    /// Files ride on the Contents permissions rather than a Files permission group of their own - an upload
    /// is how a content's images and attachments get here, so whoever may create/edit/delete content may do
    /// the same to its files (总体设计 §6.2.5). Update and Delete have to be set explicitly: FileExplorer's
    /// default for unset is "only the file's creator", which would stop one editor from replacing or removing
    /// a file another editor uploaded. CreateDirectory has to be set for the opposite reason - its default
    /// for unset is "nobody" - and goes with Create, since a directory only exists to hold uploads.
    /// <para>
    /// <c>GetFilePermissionName</c> is deliberately left unset: unset means unauthenticated reads
    /// (FileDescriptorAuthorizationHandler's own default), which a published content's files need for
    /// anonymous site visitors.
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
