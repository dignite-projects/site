using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Dignite.Site.Common;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(AdminApplicationContractsModule),
    typeof(CommonHttpApiClientModule),
    typeof(AbpHttpClientModule))]
public class AdminHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(AdminApplicationContractsModule).Assembly,
            AdminRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<AdminHttpApiClientModule>();
        });

    }
}
