using Volo.Abp.Modularity;

namespace Dignite.Sites;

[DependsOn(
    typeof(SitesApplicationModule),
    typeof(SitesDomainTestModule)
    )]
public class SitesApplicationTestModule : AbpModule
{

}
