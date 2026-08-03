using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;
using Dignite.Sites.Admin;
using Dignite.Sites.Public;

namespace Dignite.Sites;

[DependsOn(
    typeof(AdminApplicationContractsModule),
    typeof(PublicApplicationContractsModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class SitesApplicationContractsModule : AbpModule
{

}
