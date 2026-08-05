using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace Dignite.Site.Common;

[DependsOn(
    typeof(SiteDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class CommonApplicationContractsModule : AbpModule
{

}
