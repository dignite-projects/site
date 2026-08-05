using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;
using Dignite.Site.Common;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteDomainSharedModule),
    typeof(CommonApplicationContractsModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class AdminApplicationContractsModule : AbpModule
{

}
