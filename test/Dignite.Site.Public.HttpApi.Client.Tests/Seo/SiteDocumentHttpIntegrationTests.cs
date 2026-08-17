using System;
using System.Threading.Tasks;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// <see cref="SiteDocument"/> (总体设计 §5.2, §5.6, §5.9) has a single parameterized constructor and no
/// parameterless one, and until the Robots/Sitemap/Feed controllers were added it had never crossed an
/// HTTP boundary - only ever passed in-process from the app service straight to
/// <c>SiteDocumentController.ServeAsync</c>. These tests exercise the real controllers and the real
/// <b>generated</b> client proxies (<see cref="RobotsDocumentPublicClientProxy"/> and friends, resolved by
/// their concrete type - resolving by interface would let DI hand back the framework's reflection-based
/// dynamic proxy instead, which is a different code path from what was just regenerated) end to end over
/// an in-memory <see cref="TestServer"/> rather than a socket, to confirm System.Text.Json can actually
/// round-trip it, and that a null document comes back as a null reference on the client side rather than
/// an exception or an empty-but-non-null object.
/// </summary>
public class SiteDocumentHttpIntegrationTests : IDisposable
{
    private readonly ServerHost _server = new();
    private readonly ClientHost _client = new();

    public SiteDocumentHttpIntegrationTests()
    {
        _client.PointAt(_server.TestServer);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task Robots_GetRobotsTxtAsync_round_trips_ContentType_Content_and_LastModified()
    {
        var result = await _client.Resolve<RobotsDocumentPublicClientProxy>().GetRobotsTxtAsync();

        result.ShouldNotBeNull();
        result.ContentType.ShouldBe(FakeRobotsDocumentAppService.RobotsTxt.ContentType);
        result.Content.ShouldBe(FakeRobotsDocumentAppService.RobotsTxt.Content);
        result.LastModified.ShouldBe(FakeRobotsDocumentAppService.RobotsTxt.LastModified);
    }

    [Fact]
    public async Task Robots_GetLlmsTxtAsync_returns_null_over_HTTP_when_the_app_service_returns_null()
    {
        var result = await _client.Resolve<RobotsDocumentPublicClientProxy>().GetLlmsTxtAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Sitemap_GetIndexAsync_round_trips_the_document()
    {
        var result = await _client.Resolve<SitemapDocumentPublicClientProxy>().GetIndexAsync();

        result.ShouldNotBeNull();
        result.ContentType.ShouldBe(FakeSitemapDocumentAppService.Index.ContentType);
        result.Content.ShouldBe(FakeSitemapDocumentAppService.Index.Content);
    }

    [Fact]
    public async Task Sitemap_GetShardAsync_returns_the_document_for_an_existing_shard()
    {
        var result = await _client.Resolve<SitemapDocumentPublicClientProxy>()
            .GetShardAsync(FakeSitemapDocumentAppService.ExistingShardNumber);

        result.ShouldNotBeNull();
        result.Content.ShouldBe(FakeSitemapDocumentAppService.FirstShard.Content);
    }

    [Fact]
    public async Task Sitemap_GetShardAsync_returns_null_over_HTTP_for_a_shard_that_does_not_exist()
    {
        var result = await _client.Resolve<SitemapDocumentPublicClientProxy>().GetShardAsync(999);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Feed_GetAsync_returns_the_document_for_a_known_page()
    {
        var result = await _client.Resolve<FeedDocumentPublicClientProxy>()
            .GetAsync(FakeFeedDocumentAppService.KnownPagePath, "en", SiteFeedFormat.Rss);

        result.ShouldNotBeNull();
        result.Content.ShouldBe(FakeFeedDocumentAppService.BlogFeed.Content);
    }

    [Fact]
    public async Task Feed_GetAsync_returns_null_over_HTTP_for_a_page_with_no_feed()
    {
        var result = await _client.Resolve<FeedDocumentPublicClientProxy>()
            .GetAsync("/no-such-page", "en", SiteFeedFormat.Rss);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Feed_ResolveAsync_returns_the_document_for_a_known_feed_path()
    {
        var result = await _client.Resolve<FeedDocumentPublicClientProxy>()
            .ResolveAsync(FakeFeedDocumentAppService.KnownFeedPath);

        result.ShouldNotBeNull();
        result.Content.ShouldBe(FakeFeedDocumentAppService.BlogFeed.Content);
    }

    [Fact]
    public async Task Feed_ResolveAsync_returns_null_over_HTTP_for_an_unknown_feed_path()
    {
        var result = await _client.Resolve<FeedDocumentPublicClientProxy>().ResolveAsync("not-a-feed.xml");

        result.ShouldBeNull();
    }

    // AbpAspNetCoreIntegratedTestBase is obsolete in favor of AbpWebApplicationFactoryIntegratedTest, but
    // that alternative needs a real ASP.NET Core entry-point assembly (a Program class) to bootstrap from -
    // Dignite.Site.Public.HttpApi is a class library with no such entry point, so this is the base that
    // actually fits a module-only test host.
#pragma warning disable CS0618
    private sealed class ServerHost : AbpAspNetCoreIntegratedTestBase<SeoDocumentHttpServerTestModule>
    {
        public TestServer TestServer => Server;
    }
#pragma warning restore CS0618

    private sealed class ClientHost : AbpIntegratedTest<SeoDocumentHttpClientTestModule>
    {
        // The default (non-Autofac) provider never runs ABP's property injection, so every
        // LazyServiceProvider-backed property on ClientProxyBase<T> - including the ones RequestAsync
        // needs - stays null. AbpAspNetCoreIntegratedTestBase (the server side) forces Autofac internally;
        // this base does not, so it has to be requested explicitly - see SiteTestBase in
        // Dignite.Site.TestBase for the same override.
        protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
        {
            options.UseAutofac();
        }

        public T Resolve<T>() where T : notnull => GetRequiredService<T>();

        public void PointAt(TestServer server) => Resolve<ITestServerAccessor>().Server = server;
    }
}
