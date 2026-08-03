using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Sites.Common;

namespace Dignite.Sites.Public;

[DependsOn(
    typeof(SitesDomainModule),
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
