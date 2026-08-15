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
        context.Services.AddHttpClientProxies(
            typeof(SiteCommonApplicationContractsModule).Assembly,
            SiteCommonRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SiteCommonHttpApiClientModule>();
        });

    }
}
