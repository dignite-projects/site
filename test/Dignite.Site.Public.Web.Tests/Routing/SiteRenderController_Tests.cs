using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Scriban;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
using Microsoft.Extensions.DependencyInjection;
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

    private static Task<IActionResult> RenderAsync(
        SiteLanguageTestHost host,
        RouteMatchDto match,
        FakeThemeAppService? themeAppService = null)
    {
        var liveTheme = new SiteLiveThemeProvider(
            themeAppService ?? new FakeThemeAppService(null),
            new SiteLiveThemeCache(Microsoft.Extensions.Options.Options.Create(new SiteScribanOptions())),
            host.CurrentTenant);

        var controller = new SiteRenderController(
            new FakeRoutingAppService(match), new FakeFieldAppService(), new FakeHeadMetadataAppService(HeadMetadata),
            liveTheme)
        {
            ControllerContext = new ControllerContext { HttpContext = host.HttpContext, RouteData = new RouteData() },
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

    /// <summary>
    /// A page whose template the live release has renders through it - in the page's language - instead of the
    /// Razor view of the same name.
    /// </summary>
    [Fact]
    public async Task A_Template_Of_The_Live_Release_Should_Render_With_Scriban()
    {
        var scriban = new ScribanTestRenderer("en,ja", "/ja/about");
        var themes = new FakeThemeAppService(ScribanTestRenderer.RenderSet(
            "<h1>{{ page.name }}</h1>",
            "<html lang=\"{{ culture }}\"><head>{{ site_head }}</head><body>{{ body }}</body></html>").Snapshot);

        var result = await RenderAsync(scriban.Host, PageMatch("en", "ja"), themes);

        themes.Calls.ShouldBe(1);
        var scoped = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>();
        scoped.CultureName.ShouldBe("ja");
        scoped.Inner.ShouldBeOfType<ScribanPageResult>().TemplatePath.ShouldBe("Default");

        scriban.Host.HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(scriban.Renderer)
            .BuildServiceProvider();
        var body = new MemoryStream();
        scriban.Host.HttpContext.Response.Body = body;

        await result.ExecuteResultAsync(scriban.ActionContext);

        scriban.Host.HttpContext.Response.ContentType.ShouldBe("text/html; charset=utf-8");
        var html = Encoding.UTF8.GetString(body.ToArray());
        html.ShouldStartWith("<html lang=\"ja\"><head><link href=\"https://acme.example/ja/about\" rel=\"canonical\">");
        html.ShouldEndWith("<body><h1>about</h1></body></html>");
    }

    /// <summary>No template of that name in the live release: the Razor view, exactly as before.</summary>
    [Fact]
    public async Task No_Template_Of_That_Name_Should_Keep_The_Razor_View()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        var themes = new FakeThemeAppService(ScribanTestRenderer.Snapshot(
            // A layout alone does not switch a page over - only its own template does.
            new Dictionary<string, string> { ["layouts/default"] = "{{ site_head }}{{ body }}" },
            "layouts/default"));
        var match = PageMatch("en", "ja");
        match.Page!.Template = "Default.cshtml";

        var result = await RenderAsync(host, match, themes);

        var view = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ViewResult>();
        view.ViewName.ShouldBe("Default");
        view.Model.ShouldBeOfType<SiteRenderViewModel>().Page.Name.ShouldBe("about");
    }

    /// <summary>
    /// The page query parameter is the model's CurrentPage, for a Razor view's pager and a Scriban template's
    /// page_no alike; a value that is not a page number is page 1, never an error - it is the visitor's.
    /// </summary>
    [Theory]
    [InlineData("?page=3", 3)]
    [InlineData("?page=3&page=9", 3)]
    [InlineData("?page=0", 1)]
    [InlineData("?page=-2", 1)]
    [InlineData("?page=abc", 1)]
    [InlineData("?page=", 1)]
    [InlineData("?p=3", 1)]
    [InlineData("", 1)]
    public async Task The_Page_Query_Parameter_Should_Be_The_Models_Current_Page(string queryString, int expected)
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        host.HttpContext.Request.QueryString = new QueryString(queryString);

        var result = await RenderAsync(host, PageMatch("en", "ja"));

        var view = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ViewResult>();
        view.Model.ShouldBeOfType<SiteRenderViewModel>().CurrentPage.ShouldBe(expected);
    }

    [Fact]
    public async Task A_Page_Number_Beyond_The_Cap_Should_Be_The_Cap()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        host.HttpContext.Request.QueryString = new QueryString("?page=2147483647");

        var result = await RenderAsync(host, PageMatch("en", "ja"));

        var view = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ViewResult>();
        view.Model.ShouldBeOfType<SiteRenderViewModel>().CurrentPage.ShouldBe(Scriban.Functions.ContentsFunction.MaxPageNumber);
    }

    /// <summary>A tenant that never published renders every page with Razor.</summary>
    [Fact]
    public async Task Nothing_Published_Should_Keep_The_Razor_View()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");

        var result = await RenderAsync(host, PageMatch("en", "ja"), new FakeThemeAppService(null));

        result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ViewResult>();
    }

    /// <summary>A page whose Template names a static file of the theme is not rendered by Scriban.</summary>
    [Fact]
    public async Task A_Static_File_Should_Never_Render_A_Page()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        var themes = new FakeThemeAppService(ScribanTestRenderer.Snapshot(new Dictionary<string, string> { ["site.css"] = "" }));
        var match = PageMatch("en", "ja");
        match.Page!.Template = "site.css";

        var result = await RenderAsync(host, match, themes);

        result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ViewResult>();
    }

    /// <summary>A 404 never asks for the theme.</summary>
    [Fact]
    public async Task An_Unmatched_Path_Should_Not_Look_Up_The_Theme()
    {
        var host = new SiteLanguageTestHost("en", "/missing");
        var themes = new FakeThemeAppService(null);

        var result = await RenderAsync(host, new RouteMatchDto { Matched = false }, themes);

        result.ShouldBeOfType<NotFoundResult>();
        themes.Calls.ShouldBe(0);
    }

    /// <summary>
    /// A visitor holding a preview cookie for the working copy sees the page rendered with it - live release untouched,
    /// never asked for - under the banner, with the preview's headers, and told what failed if a template fails.
    /// </summary>
    [Fact]
    public async Task A_Preview_Of_The_Working_Copy_Should_Render_With_It()
    {
        var scriban = new ScribanTestRenderer("en,ja", "/ja/about");
        var themes = new FakeThemeAppService(ScribanTestRenderer.RenderSet("<h1>live</h1>", "{{ site_head }}{{ body }}").Snapshot);
        var preview = scriban.Host.Preview;
        preview.Source.WorkingCopy = ScribanTestRenderer.RenderSet(
            "<h1>draft</h1>",
            "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>").Snapshot;
        preview.SetCookie();

        var result = await RenderAsync(scriban.Host, PageMatch("en", "ja"), themes);

        themes.Calls.ShouldBe(0);
        var page = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ScribanPageResult>();
        page.ShowsErrors.ShouldBeTrue();
        page.Banner.ShouldNotBeNull();
        page.Banner.ShouldContain("Previewing the working copy");
        page.Banner.ShouldContain("/site-preview/exit?returnUrl=%2Fja%2Fabout");
        scriban.Host.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        scriban.Host.HttpContext.Response.Headers["X-Robots-Tag"].ToString().ShouldBe("noindex");

        scriban.Host.HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(scriban.Renderer)
            .BuildServiceProvider();
        var body = new MemoryStream();
        scriban.Host.HttpContext.Response.Body = body;

        await result.ExecuteResultAsync(scriban.ActionContext);

        var html = Encoding.UTF8.GetString(body.ToArray());
        html.ShouldContain("<body><div id=\"site-preview-banner\"");
        html.ShouldEndWith("<h1>draft</h1></body></html>");
    }

    /// <summary>A preview of a release reads that release; a page of it without a Scriban template still renders with Razor.</summary>
    [Fact]
    public async Task A_Preview_Of_A_Release_Without_The_Page_Template_Should_Keep_The_Razor_View()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        var releaseId = Guid.NewGuid();
        var release = ScribanTestRenderer.Snapshot(new Dictionary<string, string> { ["layouts/default"] = "{{ site_head }}{{ body }}" });
        release.ReleaseId = releaseId;
        release.ReleaseNumber = 2;
        host.Preview.Source.Releases[releaseId] = release;
        host.Preview.SetCookie(releaseId);
        var themes = new FakeThemeAppService(ScribanTestRenderer.RenderSet("<h1>live</h1>", "{{ site_head }}{{ body }}").Snapshot);

        var result = await RenderAsync(host, PageMatch("en", "ja"), themes);

        result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ViewResult>();
        themes.Calls.ShouldBe(0);
        host.Preview.Source.SnapshotRequests.ShouldBe(new Guid?[] { releaseId });
        host.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }

    /// <summary>The previewed release is gone: the live release renders, and the banner says so.</summary>
    [Fact]
    public async Task A_Preview_Of_A_Release_That_Is_Gone_Should_Render_The_Live_Release()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        host.Preview.SetCookie(Guid.NewGuid());
        var themes = new FakeThemeAppService(ScribanTestRenderer.RenderSet("<h1>live</h1>", "{{ site_head }}{{ body }}").Snapshot);

        var result = await RenderAsync(host, PageMatch("en", "ja"), themes);

        themes.Calls.ShouldBe(1);
        var page = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ScribanPageResult>();
        page.Banner!.ShouldContain("Nothing to preview");
    }

    /// <summary>A cookie of another tenant: the live release, no banner, no preview headers.</summary>
    [Fact]
    public async Task Another_Tenants_Cookie_Should_Render_The_Live_Release()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        host.Preview.Source.WorkingCopy = ScribanTestRenderer.RenderSet("<h1>draft</h1>", "{{ site_head }}{{ body }}").Snapshot;
        host.Preview.SetCookie(tenantId: Guid.NewGuid());
        var themes = new FakeThemeAppService(ScribanTestRenderer.RenderSet("<h1>live</h1>", "{{ site_head }}{{ body }}").Snapshot);

        var result = await RenderAsync(host, PageMatch("en", "ja"), themes);

        var page = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ScribanPageResult>();
        page.Banner.ShouldBeNull();
        page.ShowsErrors.ShouldBeFalse();
        host.Preview.Source.SnapshotRequests.ShouldBeEmpty();
        host.HttpContext.Response.Headers.CacheControl.ToString().ShouldBeEmpty();
    }

    /// <summary>A live release, or none (null).</summary>
    private class FakeThemeAppService(SiteThemeSnapshotDto? live) : ISiteThemePublicAppService
    {
        private readonly Guid? _releaseId = live == null ? null : Guid.NewGuid();

        public int Calls { get; private set; }

        public Task<SiteLiveThemeDto> GetLiveAsync(Guid? knownReleaseId = null)
        {
            Calls++;

            if (live == null)
            {
                return Task.FromResult(new SiteLiveThemeDto());
            }

            live.ReleaseId = _releaseId;
            return Task.FromResult(knownReleaseId == _releaseId
                ? new SiteLiveThemeDto { ReleaseId = _releaseId, IsUnchanged = true }
                : new SiteLiveThemeDto { ReleaseId = _releaseId, Snapshot = live });
        }

        /// <summary>The asset endpoint's lookup; rendering a page never makes it.</summary>
        public Task<SiteThemeAssetDto?> FindLiveAssetAsync(string path) => throw new NotSupportedException();
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
