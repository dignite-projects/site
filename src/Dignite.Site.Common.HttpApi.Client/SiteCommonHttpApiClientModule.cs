using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Site.Common;

[DependsOn(
    typeof(SiteCommonApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class SiteCommonHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // SiteCommonApplicationContractsModule's assembly has no app service interfaces today (only
        // DTOs and permission definitions), so this currently registers 0 proxies - a no-op kept
        // here for consistency with the other three *.HttpApi.Client modules. There is also no
        // ClientProxies/ directory under this project: if an app service interface is ever added to
        // Dignite.Site.Common.Application.Contracts, a static client proxy must be generated for it
        // in the same change, or AddStaticHttpClientProxies will silently skip it - unlike
        // AddHttpClientProxies (dynamic proxies), which resolves methods at call time and would just
        // cover it automatically.
        context.Services.AddStaticHttpClientProxies(
            typeof(SiteCommonApplicationContractsModule).Assembly,
            SiteCommonRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SiteCommonHttpApiClientModule>();
        });

    }
}
