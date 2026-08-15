using Dignite.FileExplorer;
using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace Dignite.Site.Common;

[DependsOn(
    typeof(SiteDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule),
    // Dignite.FileExplorer is part of Site (GitHub issue #41's follow-up) - referenced here, not from
    // Host, the same reasoning as FlexFields itself.
    typeof(FileExplorerApplicationContractsModule)
    )]
public class SiteCommonApplicationContractsModule : AbpModule
{

}
