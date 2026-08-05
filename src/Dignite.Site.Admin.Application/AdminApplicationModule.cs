using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Site.Common;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(AdminApplicationContractsModule),
    typeof(CommonApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class AdminApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<AdminApplicationModule>();
    }
}
