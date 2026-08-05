using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Site.Common;

namespace Dignite.Site.Public;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(PublicApplicationContractsModule),
    typeof(CommonApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class PublicApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<PublicApplicationModule>();
    }
}
