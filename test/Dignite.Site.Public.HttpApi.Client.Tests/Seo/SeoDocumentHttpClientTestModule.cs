using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The client side of <see cref="SiteDocumentHttpIntegrationTests"/>: the real generated
/// <c>*ClientProxy</c> classes from <see cref="SitePublicHttpApiClientModule"/>. <see cref="AbpAspNetCoreTestBaseModule"/>
/// replaces <c>IProxyHttpClientFactory</c> (which <c>ClientProxyBase&lt;T&gt;</c> resolves its
/// <c>HttpClient</c> from) with one that talks to whatever <see cref="ITestServerAccessor.Server"/> is set
/// to - the test wires that up to point at <see cref="SeoDocumentHttpServerTestModule"/>'s TestServer, so
/// calls here are genuine HTTP round trips, just over an in-memory transport instead of a socket.
/// <para>
/// <see cref="AbpRemoteServiceOptions"/> must be configured explicitly:
/// <c>RemoteServiceConfigurationDictionary.GetConfigurationOrDefault</c> throws if neither the named
/// remote service nor a "Default" entry is registered.
/// </para>
/// </summary>
[DependsOn(
    typeof(SitePublicHttpApiClientModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class SeoDocumentHttpClientTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRemoteServiceOptions>(options =>
        {
            options.RemoteServices.Default = new RemoteServiceConfiguration("http://localhost/");
        });
    }
}
