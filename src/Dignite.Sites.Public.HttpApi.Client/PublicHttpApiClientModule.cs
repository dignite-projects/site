using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Dignite.Sites.Common;

namespace Dignite.Sites.Public;

[DependsOn(
    typeof(PublicApplicationContractsModule),
    typeof(CommonHttpApiClientModule),
    typeof(AbpHttpClientModule))]
public class PublicHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(PublicApplicationContractsModule).Assembly,
            PublicRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<PublicHttpApiClientModule>();
        });

    }
}
