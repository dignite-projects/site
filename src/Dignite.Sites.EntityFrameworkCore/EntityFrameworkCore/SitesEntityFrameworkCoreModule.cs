using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.Fields;
using Dignite.Sites.Pages;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Sites.EntityFrameworkCore;

[DependsOn(
    typeof(SitesDomainModule),
    typeof(AbpEntityFrameworkCoreModule),
    // Brings the model-builder extensions, the typed index-row shape, and the EF Core base classes the
    // index manager, query executor and field repository derive from.
    typeof(FlexFieldsEntityFrameworkCoreModule)
)]
public class SitesEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<SitesDbContext>(options =>
        {
            options.AddDefaultRepositories<ISitesDbContext>(includeAllEntities: true);

            options.AddRepository<Page, EfCorePageRepository>();
            options.AddRepository<ContentType, EfCoreContentTypeRepository>();
            options.AddRepository<Field, EfCoreFieldRepository>();
            options.AddRepository<FieldGroup, EfCoreFieldGroupRepository>();
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
