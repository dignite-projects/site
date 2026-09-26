using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// The server side of <see cref="ContentListHttpIntegrationTests"/>: the real
/// <see cref="SitePublicHttpApiModule"/> pipeline, so the query string is bound by the genuine
/// <c>ContentPublicController</c>, with the app service faked out. Built the same way as
/// <c>SeoDocumentHttpServerTestModule</c> - see its remarks for why it depends on
/// <see cref="AbpAspNetCoreTestBaseModule"/>.
/// </summary>
[DependsOn(
    typeof(SitePublicHttpApiModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class ContentHttpServerTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // One instance for the whole server, so the test can read back what the controller passed it.
        context.Services.AddSingleton<FakeContentPublicAppService>();
        context.Services.AddSingleton<IContentPublicAppService>(
            serviceProvider => serviceProvider.GetRequiredService<FakeContentPublicAppService>());
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseConfiguredEndpoints();
    }
}
