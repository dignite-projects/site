using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;
using Dignite.Site.Admin;
using Dignite.Site.Public;

namespace Dignite.Site;

[DependsOn(
    typeof(SiteAdminApplicationContractsModule),
    typeof(SitePublicApplicationContractsModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class SiteApplicationContractsModule : AbpModule
{

}
