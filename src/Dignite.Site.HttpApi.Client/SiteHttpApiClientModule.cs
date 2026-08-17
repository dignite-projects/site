using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Dignite.Site.Admin;
using Dignite.Site.Public;

namespace Dignite.Site;

[DependsOn(
    typeof(SiteApplicationContractsModule),
    typeof(SiteAdminHttpApiClientModule),
    typeof(SitePublicHttpApiClientModule),
    typeof(AbpHttpClientModule))]
public class SiteHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // SiteApplicationContractsModule's assembly has no app service interfaces today (only
        // SitePermissions / SiteRemoteServiceConsts), so this currently registers 0 proxies - a
        // no-op kept here for consistency with the other three *.HttpApi.Client modules. There is
        // also no ClientProxies/ directory under this project: if an app service interface is ever
        // added to Dignite.Site.Application.Contracts, a static client proxy must be generated for it
        // in the same change, or AddStaticHttpClientProxies will silently skip it - unlike
        // AddHttpClientProxies (dynamic proxies), which resolves methods at call time and would just
        // cover it automatically.
        context.Services.AddStaticHttpClientProxies(
            typeof(SiteApplicationContractsModule).Assembly,
            SiteRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SiteHttpApiClientModule>();
        });

    }
}
