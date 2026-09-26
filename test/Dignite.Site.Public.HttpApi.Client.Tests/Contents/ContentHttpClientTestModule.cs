using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// The client side of <see cref="ContentListHttpIntegrationTests"/>: the real generated client proxies
/// from <see cref="SitePublicHttpApiClientModule"/>, talking to <see cref="ContentHttpServerTestModule"/>'s
/// TestServer. Built the same way as <c>SeoDocumentHttpClientTestModule</c> - see its remarks.
/// </summary>
[DependsOn(
    typeof(SitePublicHttpApiClientModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class ContentHttpClientTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRemoteServiceOptions>(options =>
        {
            options.RemoteServices.Default = new RemoteServiceConfiguration("http://localhost/");
        });
    }
}
