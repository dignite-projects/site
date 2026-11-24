using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Site.Admin;
using Dignite.Site.Public;

namespace Dignite.Site;

[DependsOn(
    typeof(SiteApplicationContractsModule),
    typeof(SiteAdminApplicationModule),
    typeof(SitePublicApplicationModule),
    typeof(AbpDddApplicationModule)
    )]
public class SiteApplicationModule : AbpModule
{
}
