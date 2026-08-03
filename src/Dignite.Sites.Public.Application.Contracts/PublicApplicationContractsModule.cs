using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;
using Dignite.Sites.Common;

namespace Dignite.Sites.Public;

[DependsOn(
    typeof(SitesDomainSharedModule),
    typeof(CommonApplicationContractsModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class PublicApplicationContractsModule : AbpModule
{

}
