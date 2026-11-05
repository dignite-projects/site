using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.Http.Client;
using Volo.Abp.Http.Client.ClientProxying;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// <see cref="SiteThemePreviewIntegrationClientProxy"/> is backed by a hand-maintained entry in
/// <c>site-public-generate-proxy.json</c>: this proves that entry, the controller's routes and the body binding agree, that
/// the proxy is called with its own remote service, that a site without an in-process source reads the preview's theme
/// through it - and that the endpoint does not exist unless the application exposes its integration services.
/// </summary>
public class SiteThemePreviewIntegrationHttpTests : IDisposable
{
    private readonly ClientHost _client = new();
    private readonly List<IDisposable> _servers = new();

    public void Dispose()
    {
        _client.Dispose();
        foreach (var server in _servers)
        {
            server.Dispose();
        }
    }

    private ExposedServerHost StartExposedServer()
    {
        var server = new ExposedServerHost();
        _servers.Add(server);
        _client.PointAt(server.TestServer);
        return server;
    }

    [Fact]
    public void The_Proxy_Should_Use_The_Integration_Remote_Service()
    {
        _client.Resolve<IOptions<AbpHttpClientOptions>>().Value
            .HttpClientProxies[typeof(ISiteThemePreviewIntegrationService)].RemoteServiceName
            .ShouldBe(SitePublicRemoteServiceConsts.IntegrationRemoteServiceName);

        // Every other Public service keeps the Public API's.
        _client.Resolve<IOptions<AbpHttpClientOptions>>().Value
            .HttpClientProxies[typeof(ISiteThemePublicAppService)].RemoteServiceName
            .ShouldBe(SitePublicRemoteServiceConsts.RemoteServiceName);
    }

    [Fact]
    public async Task FindSnapshotAsync_should_post_the_token_and_return_the_snapshot()
    {
        var server = StartExposedServer();
        server.Resolve<FakePreviewIntegrationService>().Snapshot = new SiteThemeSnapshotDto
        {
            DefaultLayout = "layouts/default",
            Files = new List<SiteThemeSnapshotFileDto> { new() { Path = "Default", ContentHash = "h", Body = "<p>x</p>" } }
        };

        var snapshot = await _client.Resolve<SiteThemePreviewIntegrationClientProxy>()
            .FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = "the-token" });

        server.Resolve<FakePreviewIntegrationService>().LastToken.ShouldBe("the-token");
        snapshot.ShouldNotBeNull();
        snapshot.DefaultLayout.ShouldBe("layouts/default");
        snapshot.Files.ShouldHaveSingleItem().Body.ShouldBe("<p>x</p>");
    }

    [Fact]
    public async Task A_missing_theme_should_come_back_as_null()
    {
        StartExposedServer();

        var proxy = _client.Resolve<SiteThemePreviewIntegrationClientProxy>();

        (await proxy.FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = "t" })).ShouldBeNull();
        (await proxy.FindAssetAsync(new SiteThemePreviewAssetInput { Token = "t", Path = "css/site.css" })).ShouldBeNull();
    }

    /// <summary>
    /// The site's preview, where the application has no in-process source: the remote source writes a service token for
    /// the current tenant and the release, and reads the asset through the proxy.
    /// </summary>
    [Fact]
    public async Task The_remote_source_should_read_with_a_service_token_for_the_tenant_and_the_release()
    {
        var server = StartExposedServer();
        var fake = server.Resolve<FakePreviewIntegrationService>();
        fake.Asset = new SiteThemeAssetDto { Path = "css/site.css", ContentHash = "h", Body = "body{}" };
        var releaseId = Guid.NewGuid();

        var source = _client.Resolve<ISiteThemePreviewSource>();
        source.ShouldBeOfType<SiteThemePreviewRemoteSource>();

        var asset = await source.FindAssetAsync(releaseId, "css/site.css");

        asset!.Body.ShouldBe("body{}");
        fake.LastPath.ShouldBe("css/site.css");
        _client.Resolve<SitePreviewServiceTokenProtector>().TryUnprotect(fake.LastToken, out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBeNull();
        ticket.ReleaseId.ShouldBe(releaseId);
    }

    /// <summary>
    /// ABP hides an integration service unless the application exposes them: there is no such endpoint, and the remote
    /// source reads that as no theme - the page renders live - instead of failing the page.
    /// </summary>
    [Fact]
    public async Task Without_ExposeIntegrationServices_there_should_be_no_endpoint()
    {
        var server = new HiddenServerHost();
        _servers.Add(server);
        _client.PointAt(server.TestServer);

        var exception = await Should.ThrowAsync<AbpRemoteCallException>(() =>
            _client.Resolve<SiteThemePreviewIntegrationClientProxy>()
                .FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = "t" }));
        exception.HttpStatusCode.ShouldBe(404);

        (await _client.Resolve<ISiteThemePreviewSource>().FindSnapshotAsync(null)).ShouldBeNull();
    }

#pragma warning disable CS0618
    private sealed class ExposedServerHost : AbpAspNetCoreIntegratedTestBase<ExposedServerModule>
    {
        public TestServer TestServer => Server;

        public T Resolve<T>() where T : notnull => GetRequiredService<T>();
    }

    private sealed class HiddenServerHost : AbpAspNetCoreIntegratedTestBase<HiddenServerModule>
    {
        public TestServer TestServer => Server;
    }
#pragma warning restore CS0618

    private sealed class ClientHost : AbpIntegratedTest<ClientModule>
    {
        protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
        {
            options.UseAutofac();
        }

        public T Resolve<T>() where T : notnull => GetRequiredService<T>();

        public void PointAt(TestServer server) => Resolve<ITestServerAccessor>().Server = server;
    }
}

/// <summary>The real <see cref="SitePublicHttpApiModule"/> pipeline, with the integration service faked out and exposed.</summary>
[DependsOn(
    typeof(SitePublicHttpApiModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class ExposedServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<FakePreviewIntegrationService>();
        context.Services.AddSingleton<ISiteThemePreviewIntegrationService>(
            serviceProvider => serviceProvider.GetRequiredService<FakePreviewIntegrationService>());

        Configure<AbpAspNetCoreMvcOptions>(options => options.ExposeIntegrationServices = true);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseConfiguredEndpoints();
    }
}

/// <summary>The same, without <c>ExposeIntegrationServices</c> - ABP's default.</summary>
[DependsOn(
    typeof(SitePublicHttpApiModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class HiddenServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<FakePreviewIntegrationService>();
        context.Services.AddSingleton<ISiteThemePreviewIntegrationService>(
            serviceProvider => serviceProvider.GetRequiredService<FakePreviewIntegrationService>());
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseConfiguredEndpoints();
    }
}

/// <summary>
/// The real client proxies, the integration service's remote service named apart from the Public API's, and a Data
/// Protection key ring for the remote source's tokens.
/// </summary>
[DependsOn(
    typeof(SitePublicHttpApiClientModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class ClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddDataProtection();

        Configure<AbpRemoteServiceOptions>(options =>
        {
            options.RemoteServices.Default = new RemoteServiceConfiguration("http://gateway.example/");
            options.RemoteServices[SitePublicRemoteServiceConsts.IntegrationRemoteServiceName] =
                new RemoteServiceConfiguration("http://localhost/");
        });
    }
}

/// <summary>Records what the controller passed it, and answers what the test set.</summary>
public class FakePreviewIntegrationService : ISiteThemePreviewIntegrationService
{
    public SiteThemeSnapshotDto? Snapshot { get; set; }

    public SiteThemeAssetDto? Asset { get; set; }

    public string? LastToken { get; private set; }

    public string? LastPath { get; private set; }

    public Task<SiteThemeSnapshotDto?> FindSnapshotAsync(SiteThemePreviewSnapshotInput input)
    {
        LastToken = input.Token;
        return Task.FromResult(Snapshot);
    }

    public Task<SiteThemeAssetDto?> FindAssetAsync(SiteThemePreviewAssetInput input)
    {
        LastToken = input.Token;
        LastPath = input.Path;
        return Task.FromResult(Asset);
    }
}
