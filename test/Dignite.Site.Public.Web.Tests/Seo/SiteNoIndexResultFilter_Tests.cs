using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Preview;
using Dignite.Site.Public.Routing;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// <c>X-Robots-Tag: noindex</c> on what a site closed to indexing serves, whatever its template writes in the head.
/// </summary>
public class SiteNoIndexResultFilter_Tests
{
    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    public async Task A_Site_Closed_To_Indexing_Should_Send_NoIndex(string allowIndexing)
    {
        var response = await RunAsync(allowIndexing);

        response.Headers[SitePreviewHttp.RobotsTagHeaderName].ToString().ShouldBe("noindex");
    }

    [Fact]
    public async Task An_Open_Site_Should_Send_No_Robots_Header()
    {
        var response = await RunAsync("true");

        response.Headers.ContainsKey(SitePreviewHttp.RobotsTagHeaderName).ShouldBeFalse();
    }

    /// <summary>A front end that cannot read the setting must not take an open site out of an index.</summary>
    [Fact]
    public async Task An_Unreadable_Setting_Should_Leave_The_Site_Open()
    {
        var response = await RunAsync(allowIndexing: null);

        response.Headers.ContainsKey(SitePreviewHttp.RobotsTagHeaderName).ShouldBeFalse();
    }

    /// <summary>
    /// Pages, the sitemap, the feeds and <c>llms.txt</c> carry the header; <c>robots.txt</c>, the crawl rules
    /// themselves, does not.
    /// </summary>
    [Fact]
    public void The_Filter_Should_Apply_To_The_Pages_And_Documents_But_Not_RobotsTxt()
    {
        AppliesTo(typeof(SiteRenderController)).ShouldBeTrue();
        AppliesTo(typeof(SitemapController)).ShouldBeTrue();
        AppliesTo(typeof(FeedController)).ShouldBeTrue();

        AppliesTo(typeof(RobotsController).GetMethod(nameof(RobotsController.GetLlmsTxtAsync))!).ShouldBeTrue();
        AppliesTo(typeof(RobotsController)).ShouldBeFalse();
        AppliesTo(typeof(RobotsController).GetMethod(nameof(RobotsController.GetRobotsTxtAsync))!).ShouldBeFalse();
    }

    private static bool AppliesTo(MemberInfo member)
    {
        return member.GetCustomAttributes<ServiceFilterAttribute>(inherit: true)
            .Any(attribute => attribute.ServiceType == typeof(SiteNoIndexResultFilter));
    }

    private static async Task<HttpResponse> RunAsync(string? allowIndexing)
    {
        var settings = new FakeSettingProvider { [SiteSettings.Robots.AllowIndexing] = allowIndexing };
        var filter = new SiteNoIndexResultFilter(settings);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ResultExecutingContext(actionContext, new List<IFilterMetadata>(), new OkResult(), new object());

        var nextCalled = false;
        await filter.OnResultExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ResultExecutedContext(actionContext, new List<IFilterMetadata>(), context.Result, new object()));
        });

        nextCalled.ShouldBeTrue();

        return httpContext.Response;
    }
}
