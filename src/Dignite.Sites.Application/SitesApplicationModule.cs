using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Sites.Admin;
using Dignite.Sites.Public;

namespace Dignite.Sites;

[DependsOn(
    typeof(SitesApplicationContractsModule),
    typeof(AdminApplicationModule),
    typeof(PublicApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class SitesApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SitesApplicationModule>();
    }
}
