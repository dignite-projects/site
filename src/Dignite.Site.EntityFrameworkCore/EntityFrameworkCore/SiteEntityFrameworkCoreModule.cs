using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Dignite.FileExplorer.EntityFrameworkCore;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Site.EntityFrameworkCore;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(AbpEntityFrameworkCoreModule),
    // Brings the model-builder extensions, the typed index-row shape, and the EF Core base classes the
    // index manager, query executor and field repository derive from.
    typeof(FlexFieldsEntityFrameworkCoreModule),
    // Dignite.FileExplorer's backend, in-process (GitHub issue #41). Registers FileExplorerDbContext,
    // which shares the host's default connection string the same way Identity/OpenIddict/etc. already do
    // - nothing here points it at a second database. FileDescriptor/DirectoryDescriptor are plain
    // IMultiTenant, so whatever ICurrentTenant is active for the request is what a file gets tagged with;
    // no tenant-mapping code needed.
    typeof(FileExplorerEntityFrameworkCoreModule)
)]
public class SiteEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<SiteDbContext>(options =>
        {
            options.AddDefaultRepositories<ISiteDbContext>(includeAllEntities: true);

            options.AddRepository<Page, EfCorePageRepository>();
            options.AddRepository<ContentType, EfCoreContentTypeRepository>();
            options.AddRepository<Field, EfCoreFieldRepository>();
            options.AddRepository<Content, EfCoreContentRepository>();
        });

        // ABP's conventional registrar exposes a class as any interface whose name, minus the leading
        // "I", the class name ends with. "EfCoreFieldRepository" ends with "FieldRepository", so
        // IFieldRepository is covered - but IFlexFieldRepository<Field> would need the class to be
        // named "...FlexFieldRepository", which it is not. The kernel documents this and expects the
        // one explicit line; without it, anything resolving the kernel's own interface gets nothing.
        context.Services.AddTransient<IFlexFieldRepository<Field>, EfCoreFieldRepository>();
    }
}
