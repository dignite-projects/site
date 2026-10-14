using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;
using Volo.Abp.Features;
using Dignite.Site.Common;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteDomainSharedModule),
    typeof(SiteCommonApplicationContractsModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpFeaturesModule)
    )]
public class SiteAdminApplicationContractsModule : AbpModule
{

}
