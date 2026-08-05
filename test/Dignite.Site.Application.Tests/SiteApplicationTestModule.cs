using Volo.Abp.Modularity;

namespace Dignite.Site;

[DependsOn(
    typeof(SiteApplicationModule),
    typeof(SiteDomainTestModule)
    )]
public class SiteApplicationTestModule : AbpModule
{

}
