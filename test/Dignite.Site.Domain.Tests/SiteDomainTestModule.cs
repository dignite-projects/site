using Volo.Abp.Modularity;

namespace Dignite.Site;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(SiteTestBaseModule)
)]
public class SiteDomainTestModule : AbpModule
{

}
