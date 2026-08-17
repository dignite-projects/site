using System;
using System.Linq;
using System.Reflection;
using Dignite.Site.Admin;
using Dignite.Site.Common;
using Dignite.Site.Public;
using Shouldly;
using Volo.Abp.Application.Services;
using Volo.Abp.Http.Client.ClientProxying;
using Xunit;

namespace Dignite.Site;

/// <summary>
/// Structural invariant behind the <c>AddHttpClientProxies</c> -&gt; <c>AddStaticHttpClientProxies</c>
/// switch across all four <c>*.HttpApi.Client</c> modules: unlike dynamic proxies, which discover
/// methods from the server's <c>/api/abp/api-definition</c> at call time, static proxies only cover
/// whatever was generated ahead of time. For each <c>*.Application.Contracts</c> assembly, this
/// asserts the set of app service interfaces exactly matches the set of interfaces the corresponding
/// <c>*.HttpApi.Client</c> assembly has a generated <see cref="ClientProxyBase{T}"/> proxy for - so
/// adding an app service without regenerating its client proxy fails a test instead of silently
/// 404ing behind the API gateway. This also covers Common and the unified module, which have no
/// ClientProxies/ directory at all today (see the comments on their modules' ConfigureServices): if
/// either ever gains an app service interface, this goes red until a proxy is generated for it.
/// <para>
/// The reverse direction - a leftover proxy for a removed interface - needs no runtime check: a
/// generated proxy declared as <c>: ClientProxyBase&lt;IRemoved&gt;, IRemoved</c> for a deleted
/// interface simply fails to compile.
/// </para>
/// </summary>
public class ClientProxyCoverageTests
{
    [Fact]
    public void Admin_client_proxies_cover_exactly_the_Admin_app_service_interfaces()
    {
        AssertFullCoverage(typeof(SiteAdminApplicationContractsModule).Assembly, typeof(SiteAdminHttpApiClientModule).Assembly);
    }

    [Fact]
    public void Common_client_proxies_cover_exactly_the_Common_app_service_interfaces()
    {
        AssertFullCoverage(typeof(SiteCommonApplicationContractsModule).Assembly, typeof(SiteCommonHttpApiClientModule).Assembly);
    }

    [Fact]
    public void Public_client_proxies_cover_exactly_the_Public_app_service_interfaces()
    {
        AssertFullCoverage(typeof(SitePublicApplicationContractsModule).Assembly, typeof(SitePublicHttpApiClientModule).Assembly);
    }

    [Fact]
    public void Unified_client_proxies_cover_exactly_the_unified_app_service_interfaces()
    {
        AssertFullCoverage(typeof(SiteApplicationContractsModule).Assembly, typeof(SiteHttpApiClientModule).Assembly);
    }

    private static void AssertFullCoverage(Assembly contractsAssembly, Assembly clientAssembly)
    {
        var appServiceInterfaces = contractsAssembly.GetTypes()
            .Where(t => t.IsInterface && t.IsPublic && typeof(IApplicationService).IsAssignableFrom(t) && t != typeof(IApplicationService))
            .ToHashSet();

        var proxyCoveredInterfaces = clientAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Select(GetClientProxyTargetInterface)
            .OfType<Type>()
            .ToHashSet();

        proxyCoveredInterfaces.ShouldBe(appServiceInterfaces, ignoreOrder: true);
    }

    /// <summary>
    /// The interface a generated <c>*ClientProxy : ClientProxyBase&lt;TInterface&gt;, TInterface</c>
    /// covers, or <see langword="null"/> for any type that isn't a generated static client proxy.
    /// </summary>
    private static Type? GetClientProxyTargetInterface(Type type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ClientProxyBase<>))
            {
                return baseType.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
