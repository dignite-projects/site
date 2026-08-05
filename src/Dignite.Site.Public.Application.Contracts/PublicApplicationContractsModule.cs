using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;
using Dignite.Site.Common;

namespace Dignite.Site.Public;

[DependsOn(
    typeof(SiteDomainSharedModule),
    typeof(CommonApplicationContractsModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class PublicApplicationContractsModule : AbpModule
{

}
