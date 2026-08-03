using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Dignite.Sites.Admin;
using Dignite.Sites.Public;

namespace Dignite.Sites;

[DependsOn(
    typeof(SitesApplicationContractsModule),
    typeof(AdminHttpApiClientModule),
    typeof(PublicHttpApiClientModule),
    typeof(AbpHttpClientModule))]
public class SitesHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(SitesApplicationContractsModule).Assembly,
            SitesRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SitesHttpApiClientModule>();
        });

    }
}
