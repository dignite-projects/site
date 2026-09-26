using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// <see cref="GetContentListInput.FlexFieldConditions"/> is a list of objects on a GET endpoint, so it
/// travels in the query string. <c>ContentListTagHelper</c> puts a route's field filters there - the
/// <c>news</c> of <c>/blog/news</c> against <c>/blog/{blog_category?}</c> - and in a host that calls Site
/// over HTTP instead of in-process, every one of them has to survive the generated client proxy
/// (<see cref="ContentPublicClientProxy"/>, resolved by its concrete type for the reason given on
/// <c>SiteDocumentHttpIntegrationTests</c>) and the controller's model binding, or the list silently
/// comes back unfiltered.
/// </summary>
public class ContentListHttpIntegrationTests : IDisposable
{
    private readonly ServerHost _server = new();
    private readonly ClientHost _client = new();

    public ContentListHttpIntegrationTests()
    {
        _client.PointAt(_server.TestServer);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task GetListAsync_delivers_every_FlexFieldCondition_to_the_server_intact()
    {
        var pageId = Guid.NewGuid();
        var sent = new List<FlexFieldQueryCondition>
        {
            // Reserved and non-ASCII characters in the value, to prove it is escaped and unescaped as one value.
            new(Guid.NewGuid(), "blog_category", FlexFieldQueryOperator.Equals, "news & views/新闻 +1=?", FlexFieldValueType.String),
            new(Guid.NewGuid(), "event-date", FlexFieldQueryOperator.GreaterThanOrEqual, "2025-08-01T00:00:00.0000000Z", FlexFieldValueType.DateTime)
        };

        await _client.Resolve<ContentPublicClientProxy>().GetListAsync(new GetContentListInput
        {
            PageId = pageId,
            CultureName = "en",
            MaxResultCount = 50,
            FlexFieldConditions = sent
        });

        var received = _server.Resolve<FakeContentPublicAppService>().LastListInput;
        received.ShouldNotBeNull();
        received.PageId.ShouldBe(pageId);
        received.CultureName.ShouldBe("en");
        received.MaxResultCount.ShouldBe(50);
        received.FlexFieldConditions.ShouldNotBeNull();
        received.FlexFieldConditions.Count.ShouldBe(sent.Count);
        for (var i = 0; i < sent.Count; i++)
        {
            received.FlexFieldConditions[i].FieldId.ShouldBe(sent[i].FieldId);
            received.FlexFieldConditions[i].FieldName.ShouldBe(sent[i].FieldName);
            received.FlexFieldConditions[i].Operator.ShouldBe(sent[i].Operator);
            received.FlexFieldConditions[i].Value.ShouldBe(sent[i].Value);
            received.FlexFieldConditions[i].ValueType.ShouldBe(sent[i].ValueType);
        }
    }

    [Fact]
    public async Task GetListAsync_with_an_empty_condition_list_sends_the_other_parameters_untouched()
    {
        var pageId = Guid.NewGuid();

        await _client.Resolve<ContentPublicClientProxy>().GetListAsync(new GetContentListInput
        {
            PageId = pageId,
            CultureName = "en",
            FlexFieldConditions = new List<FlexFieldQueryCondition>()
        });

        var received = _server.Resolve<FakeContentPublicAppService>().LastListInput;
        received.ShouldNotBeNull();
        received.PageId.ShouldBe(pageId);
        received.CultureName.ShouldBe("en");
        (received.FlexFieldConditions ?? new List<FlexFieldQueryCondition>()).ShouldBeEmpty();
    }

    // See SiteDocumentHttpIntegrationTests for why this base, and why the client host forces Autofac.
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
