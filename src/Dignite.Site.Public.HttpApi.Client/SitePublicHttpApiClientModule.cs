using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Dignite.Site.Common;

namespace Dignite.Site.Public;

[DependsOn(
    typeof(SitePublicApplicationContractsModule),
    typeof(SiteCommonHttpApiClientModule),
    typeof(AbpHttpClientModule))]
public class SitePublicHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(SitePublicApplicationContractsModule).Assembly,
            SitePublicRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SitePublicHttpApiClientModule>();
        });

    }
}
