using Dignite.FileExplorer;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;

namespace Dignite.Site.Common;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(SiteCommonApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    // Dignite.FileExplorer's own Application layer (GitHub issue #41's follow-up) - unmodified, own
    // permission model. Site calls FileDescriptorManager directly for its own field-type wiring; this is
    // what FileExplorer's own Angular picker calls.
    typeof(FileExplorerApplicationModule)
    )]
public class SiteCommonApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SiteCommonApplicationModule>();
    }
}
