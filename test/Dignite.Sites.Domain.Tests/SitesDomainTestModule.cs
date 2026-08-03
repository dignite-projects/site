using Volo.Abp.Modularity;

namespace Dignite.Sites;

[DependsOn(
    typeof(SitesDomainModule),
    typeof(SitesTestBaseModule)
)]
public class SitesDomainTestModule : AbpModule
{

}
