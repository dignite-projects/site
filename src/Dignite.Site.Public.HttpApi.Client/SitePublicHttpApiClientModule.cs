using Dignite.Site.Public.Themes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Http.Client;
using Volo.Abp.Http.Client.Proxying;
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
        context.Services.AddStaticHttpClientProxies(
            typeof(SitePublicApplicationContractsModule).Assembly,
            SitePublicRemoteServiceConsts.RemoteServiceName
        );

        // The integration services are called with a remote service of their own: a gateway in front of the Public API
        // does not forward /integration-api/, so they are reached at the application's own address.
        Configure<AbpHttpClientOptions>(options =>
        {
            options.HttpClientProxies[typeof(ISiteThemePreviewIntegrationService)] = new HttpClientProxyConfig(
                typeof(ISiteThemePreviewIntegrationService),
                SitePublicRemoteServiceConsts.IntegrationRemoteServiceName);
        });

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SitePublicHttpApiClientModule>();
        });

    }

    /// <summary>
    /// The site's preview reads its theme over HTTP where the application has no in-process source - a site rendered
    /// against a remote Public API. After every module has registered its services, so an application that hosts
    /// <c>Dignite.Site.Public.Application</c> too keeps reading its own database.
    /// </summary>
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<ISiteThemePreviewSource, SiteThemePreviewRemoteSource>();
    }
}
