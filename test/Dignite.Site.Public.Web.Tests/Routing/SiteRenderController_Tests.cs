using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// What <see cref="SiteRenderController"/> hands the page's language helpers (GitHub issue #75): the site's
/// languages from the route match, and the head metadata the switcher reads - only for a page it renders.
/// </summary>
public class SiteRenderController_Tests
{
    private static readonly HeadMetadataDto HeadMetadata = new()
    {
        MetaTitle = "About",
        BaseMetaTitle = "About",
        CanonicalUrl = "https://acme.example/ja/about"
    };

    private static RouteMatchDto PageMatch(params string[] enabledCultureNames) => new()
    {
        Matched = true,
        Kind = RouteMatchKindDto.Page,
        CultureName = "ja",
        DefaultCultureName = "en",
        EnabledCultureNames = new List<string>(enabledCultureNames),
        Page = new PageDto { Name = "about", Template = "Default" }
    };

    private static Task<IActionResult> RenderAsync(SiteLanguageTestHost host, RouteMatchDto match)
    {
        var controller = new SiteRenderController(
            new FakeRoutingAppService(match), new FakeFieldAppService(), new FakeHeadMetadataAppService(HeadMetadata))
        {
            ControllerContext = new ControllerContext { HttpContext = host.HttpContext },
            TempData = new TempDataDictionary(host.HttpContext, new NullTempDataProvider()),
            LazyServiceProvider = new AbpLazyServiceProvider(host.HttpContext.RequestServices)
        };

        return controller.RenderAsync();
    }

    /// <summary>
    /// The match already carries the languages it was resolved with; the setting is not read a second time,
    /// and a synchronous <c>Url.SiteContent</c> in the page's view works.
    /// </summary>
    [Fact]
    public async Task Should_Supply_The_Matchs_Languages_Without_Reading_The_Setting()
    {
        var host = new SiteLanguageTestHost("en", "/ja/about");

        var result = await RenderAsync(host, PageMatch("en", "ja"));

        result.ShouldNotBeOfType<NotFoundResult>();
        host.Settings.ReadCount.ShouldBe(0);
        host.LanguageContext.Languages!.EnabledCultureNames.ShouldBe(new[] { "en", "ja" });
        host.LanguageContext.HeadMetadata.ShouldBeSameAs(HeadMetadata);

        new UrlHelper(new ActionContext(host.HttpContext, new RouteData(), new ActionDescriptor()))
            .SiteContent("~/contact").ShouldBe("/ja/contact");
    }

    /// <summary>A server too old to send the list leaves the loading to SiteLanguageResultFilter.</summary>
    [Fact]
    public async Task A_Match_Without_The_List_Should_Leave_Loading_To_The_Filter()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");

        await RenderAsync(host, PageMatch());

        host.LanguageContext.Languages.ShouldBeNull();
        host.LanguageContext.HeadMetadata.ShouldBeSameAs(HeadMetadata);
    }

    /// <summary>
    /// A matched route that cannot render - its content type deleted underneath it - is a 404, and must not
    /// leave a re-executed error page offering translations of a page that was never shown.
    /// </summary>
    [Fact]
    public async Task A_Page_That_Does_Not_Render_Should_Leave_No_Head_Metadata()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about/my-trip");
        var match = PageMatch("en", "ja");
        match.Kind = RouteMatchKindDto.Content;

        var result = await RenderAsync(host, match);

        result.ShouldBeOfType<NotFoundResult>();
        host.LanguageContext.HeadMetadata.ShouldBeNull();
    }

    private class FakeRoutingAppService(RouteMatchDto match) : IRoutingPublicAppService
    {
        public Task<RouteMatchDto> ResolveAsync(ResolvePathInput input) => Task.FromResult(match);
    }

    private class FakeHeadMetadataAppService(HeadMetadataDto headMetadata) : IHeadMetadataPublicAppService
    {
        public Task<HeadMetadataDto?> ResolveAsync(ResolveHeadMetadataInput input) => Task.FromResult<HeadMetadataDto?>(headMetadata);
    }

    private class FakeFieldAppService : IFieldPublicAppService
    {
        public Task<FieldDto> GetAsync(Guid id) => throw new NotSupportedException();

        public Task<ListResultDto<FieldDto>> GetListAsync(IEnumerable<Guid> ids) => throw new NotSupportedException();
    }

    private class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
