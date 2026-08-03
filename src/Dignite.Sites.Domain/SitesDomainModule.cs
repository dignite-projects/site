using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.Sites;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(SitesDomainSharedModule)
)]
public class SitesDomainModule : AbpModule
{

}
