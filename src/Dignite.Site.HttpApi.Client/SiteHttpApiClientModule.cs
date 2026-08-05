using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Dignite.Site.Admin;
using Dignite.Site.Public;

namespace Dignite.Site;

[DependsOn(
    typeof(SiteApplicationContractsModule),
    typeof(AdminHttpApiClientModule),
    typeof(PublicHttpApiClientModule),
    typeof(AbpHttpClientModule))]
public class SiteHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(SiteApplicationContractsModule).Assembly,
            SiteRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SiteHttpApiClientModule>();
        });

    }
}
