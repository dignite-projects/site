using Dignite.Site.Public.Contents;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Regression lock for the <c>AddHttpClientProxies</c> -&gt; <c>AddStaticHttpClientProxies</c> switch in
/// <see cref="SitePublicHttpApiClientModule"/>: resolves an app service by its *interface* - the way a
/// real consumer injects it - and asserts the concrete type behind it is the generated
/// <c>*ClientProxy</c>, not a Castle dynamic proxy (typically <c>Castle.Proxies.*</c>). Resolving by
/// concrete type instead, as <see cref="SiteDocumentHttpIntegrationTests"/> does, would not catch a
/// regression back to dynamic proxies: both registrations satisfy an interface-typed injection site
/// equally well, so only the runtime type of what comes back tells them apart.
/// <para>
/// Client-only: unlike <see cref="SiteDocumentHttpIntegrationTests"/>, this never points
/// <see cref="Volo.Abp.AspNetCore.TestBase.ITestServerAccessor"/> at a server or performs an HTTP
/// call, because resolving the service and inspecting its type doesn't need one - reusing
/// <see cref="SeoDocumentHttpClientTestModule"/> here is only for its
/// <see cref="Volo.Abp.Http.Client.AbpRemoteServiceOptions"/> setup, not its server-pointing.
/// </para>
/// </summary>
public class StaticClientProxyRegistrationTests
{
    [Fact]
    public void Public_module_resolves_IRobotsDocumentAppService_to_the_generated_static_proxy()
    {
        using var client = new ClientHost();

        var resolved = client.Resolve<IRobotsDocumentAppService>();

        resolved.ShouldBeOfType<RobotsDocumentPublicClientProxy>();
    }

    [Fact]
    public void Public_module_resolves_IContentPublicAppService_to_the_generated_static_proxy()
    {
        using var client = new ClientHost();

        var resolved = client.Resolve<IContentPublicAppService>();

        resolved.ShouldBeOfType<ContentPublicClientProxy>();
    }

    private sealed class ClientHost : AbpIntegratedTest<SeoDocumentHttpClientTestModule>
    {
        // Same reasoning as SiteDocumentHttpIntegrationTests.ClientHost: the default (non-Autofac)
        // provider never runs ABP's property injection, which ClientProxyBase<T> instances rely on.
        protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
        {
            options.UseAutofac();
        }

        public T Resolve<T>() where T : notnull => GetRequiredService<T>();
    }
}
