using System;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// <see cref="ContentPublicClientProxy.GetAdjacentAsync"/> is backed by a hand-maintained entry in
/// <c>site-public-generate-proxy.json</c>; this proves that entry, the route and the controller's model
/// binding agree - the id in the path, the optional content type in the query string, and a missing
/// neighbor coming back as a null side of the DTO rather than failing the call.
/// </summary>
public class ContentAdjacentHttpIntegrationTests : IDisposable
{
    private readonly ServerHost _server = new();
    private readonly ClientHost _client = new();

    public ContentAdjacentHttpIntegrationTests()
    {
        _client.PointAt(_server.TestServer);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task GetAdjacentAsync_delivers_the_id_and_content_type_and_returns_both_sides()
    {
        var id = Guid.NewGuid();
        var contentTypeId = Guid.NewGuid();
        var nextId = Guid.NewGuid();
        _server.Resolve<FakeContentPublicAppService>().AdjacentResult = new AdjacentContentsDto
        {
            Previous = null,
            Next = new ContentDto { Id = nextId, Slug = "newer", Url = "/blog/newer" }
        };

        var result = await _client.Resolve<ContentPublicClientProxy>().GetAdjacentAsync(
            id, new GetAdjacentContentsInput { ContentTypeId = contentTypeId });

        var fake = _server.Resolve<FakeContentPublicAppService>();
        fake.LastAdjacentId.ShouldBe(id);
        fake.LastAdjacentInput.ShouldNotBeNull();
        fake.LastAdjacentInput.ContentTypeId.ShouldBe(contentTypeId);

        result.Previous.ShouldBeNull();
        result.Next.ShouldNotBeNull();
        result.Next.Id.ShouldBe(nextId);
        result.Next.Url.ShouldBe("/blog/newer");
    }

    [Fact]
    public async Task GetAdjacentAsync_without_a_content_type_binds_it_as_null()
    {
        var id = Guid.NewGuid();

        await _client.Resolve<ContentPublicClientProxy>().GetAdjacentAsync(id, new GetAdjacentContentsInput());

        var fake = _server.Resolve<FakeContentPublicAppService>();
        fake.LastAdjacentId.ShouldBe(id);
        fake.LastAdjacentInput.ShouldNotBeNull();
        fake.LastAdjacentInput.ContentTypeId.ShouldBeNull();
    }

    // Same hosts as ContentListHttpIntegrationTests - see SiteDocumentHttpIntegrationTests for why.
#pragma warning disable CS0618
    private sealed class ServerHost : AbpAspNetCoreIntegratedTestBase<ContentHttpServerTestModule>
    {
        public TestServer TestServer => Server;

        public T Resolve<T>() where T : notnull => GetRequiredService<T>();
    }
#pragma warning restore CS0618

    private sealed class ClientHost : AbpIntegratedTest<ContentHttpClientTestModule>
    {
        protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
        {
            options.UseAutofac();
        }

        public T Resolve<T>() where T : notnull => GetRequiredService<T>();

        public void PointAt(TestServer server) => Resolve<ITestServerAccessor>().Server = server;
    }
}
