using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The server side of <see cref="SiteDocumentHttpIntegrationTests"/>: the real
/// <see cref="SitePublicHttpApiModule"/> pipeline (so the new Robots/Sitemap/Feed controllers are the
/// genuine article), with the three app services faked out so the test needs no database.
/// </summary>
/// <remarks>
/// Depends on <see cref="AbpAspNetCoreTestBaseModule"/> even though nothing here makes outgoing proxied
/// calls: <c>AbpAspNetCoreIntegratedTestBase&lt;T&gt;</c>'s own constructor unconditionally resolves
/// <see cref="ITestServerAccessor"/> to stash the TestServer it just built, so the module supplying that
/// registration has to be loaded on this side too, not just the client side that actually reads it back.
/// </remarks>
[DependsOn(
    typeof(SitePublicHttpApiModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class SeoDocumentHttpServerTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IRobotsDocumentAppService, FakeRobotsDocumentAppService>();
        context.Services.AddTransient<ISitemapDocumentAppService, FakeSitemapDocumentAppService>();
        context.Services.AddTransient<IFeedDocumentAppService, FakeFeedDocumentAppService>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseConfiguredEndpoints();
    }
}
