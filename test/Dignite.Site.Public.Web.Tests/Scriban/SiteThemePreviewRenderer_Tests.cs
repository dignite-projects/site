using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Themes;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// <see cref="SiteThemePreviewRenderer"/> over a real <see cref="ScribanPageRenderer"/> (<see cref="ScribanTestRenderer"/>),
/// with the Public application services it calls faked: the page a path resolves to, rendered with the given theme
/// snapshot in place of the live release. The request is the admin API call that asked for the preview, not the page.
/// </summary>
public class SiteThemePreviewRenderer_Tests
{
    private const string Layout =
        "<html lang=\"{{ culture }}\"><head>{{ site_head }}</head><body>{{ body }}</body></html>";

    private static RouteMatchDto PageMatch(string template = "Default") => new()
    {
        Matched = true,
        Kind = RouteMatchKindDto.Page,
        CultureName = "ja",
        DefaultCultureName = "en",
        EnabledCultureNames = new List<string> { "en", "ja" },
        Page = new PageDto { Name = "about", Template = template }
    };

    /// <summary>A theme with <paramref name="page"/> at Default and the standard layout as its default layout.</summary>
    private static SiteThemeSnapshotDto Theme(string page, Dictionary<string, string>? others = null)
    {
        var files = new Dictionary<string, string> { ["Default"] = page, ["layouts/default"] = Layout };
        foreach (var (path, body) in others ?? new Dictionary<string, string>())
        {
            files[path] = body;
        }

        return ScribanTestRenderer.Snapshot(files, "layouts/default");
    }

    private sealed class Fixture
    {
        public Fixture(RouteMatchDto match, SiteScribanOptions? options = null)
        {
            Scriban = new ScribanTestRenderer("en,ja", "/api/site-admin/theme/preview", options);
            Routing = new FakeRoutingAppService(match);
            HeadMetadata = new FakeHeadMetadataAppService(ScribanTestRenderer.HeadMetadata);

            Renderer = new SiteThemePreviewRenderer(
                Routing,
                new FakeFieldAppService(),
                HeadMetadata,
                Scriban.Renderer,
                Scriban.Host.LanguageContext,
                new HttpContextAccessor { HttpContext = Scriban.Host.HttpContext },
                Scriban.Options);
        }

        public ScribanTestRenderer Scriban { get; }

        public FakeRoutingAppService Routing { get; }

        public FakeHeadMetadataAppService HeadMetadata { get; }

        public SiteThemePreviewRenderer Renderer { get; }

        public Task<SiteThemePreviewDto> RenderAsync(SiteThemeSnapshotDto snapshot, string path = "/ja/about")
        {
            return Renderer.RenderAsync(new SiteThemePreviewRequest { Path = path, Snapshot = snapshot });
        }
    }

    [Fact]
    public async Task A_Page_Should_Render_With_The_Snapshot_In_Its_Layout()
    {
        var fixture = new Fixture(PageMatch());

        var preview = await fixture.RenderAsync(Theme("<h1>preview {{ page.name }}</h1>"));

        preview.Html.ShouldStartWith("<html lang=\"ja\"><head>");
        preview.Html.ShouldEndWith("<body><h1>preview about</h1></body></html>");
        preview.Layout.ShouldBe("layouts/default");
    }

    [Fact]
    public async Task A_Page_Should_Render_In_The_Layout_Its_Front_Matter_Names()
    {
        var fixture = new Fixture(PageMatch());

        var preview = await fixture.RenderAsync(Theme(
            "+++\nlayout = \"layouts/landing\"\n+++\n<p>x</p>",
            new() { ["layouts/landing"] = "{{ site_head }}<section>{{ body }}</section>" }));

        preview.Layout.ShouldBe("layouts/landing");
        preview.Html.ShouldEndWith("<section><p>x</p></section>");
    }

    /// <summary>A page whose template is not in the snapshot renders through Razor live, which a preview cannot show.</summary>
    [Fact]
    public async Task A_Page_Rendered_By_Razor_Should_Throw_Not_Scriban()
    {
        var fixture = new Fixture(PageMatch("Default.cshtml"));

        var exception = await Should.ThrowAsync<BusinessException>(
            () => fixture.RenderAsync(ScribanTestRenderer.Snapshot(new Dictionary<string, string> { ["layouts/default"] = Layout }, "layouts/default")));

        exception.Code.ShouldBe(SiteErrorCodes.SiteThemePreviewPageNotScriban);
        exception.Data["Template"].ShouldBe("Default");
        exception.Data["Path"].ShouldBe("/ja/about");
    }

    [Fact]
    public async Task A_Path_No_Page_Answers_Should_Throw_Path_Not_Found()
    {
        var fixture = new Fixture(new RouteMatchDto { Matched = false, CultureName = "en" });

        var exception = await Should.ThrowAsync<BusinessException>(() => fixture.RenderAsync(Theme("{{ site_head }}"), "/missing"));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewPathNotFound);
        exception.Data["Path"].ShouldBe("/missing");
    }

    [Theory]
    [InlineData("ja/about", "/ja/about")]
    [InlineData(" /ja/about ", "/ja/about")]
    [InlineData("", "/")]
    public async Task The_Path_Should_Be_Normalized(string path, string expected)
    {
        var fixture = new Fixture(PageMatch());

        var preview = await fixture.RenderAsync(Theme("<p>{{ page.name }}</p>"), path);

        fixture.Routing.RequestedPaths.ShouldBe(new[] { expected });
        fixture.HeadMetadata.RequestedPaths.ShouldBe(new[] { expected });
        preview.Path.ShouldBe(expected);
    }

    /// <summary>
    /// A query string on the path is the page of a paged list, as on a visitor's request: the path alone is resolved
    /// and rendered as, and the page number reaches the template, site_head and page_url.
    /// </summary>
    [Fact]
    public async Task A_Query_String_On_The_Path_Should_Give_The_Page_Number()
    {
        var fixture = new Fixture(PageMatch());

        var preview = await fixture.RenderAsync(
            Theme("<p>{{ page_no }}|{{ page_url 1 }}|{{ page_url 3 }}</p>"), "/ja/about?page=2&x=1");

        fixture.Routing.RequestedPaths.ShouldBe(new[] { "/ja/about" });
        preview.Path.ShouldBe("/ja/about");
        preview.Html.ShouldContain("<link href=\"https://acme.example/ja/about?page=2\" rel=\"canonical\">");
        preview.Html.ShouldEndWith("<p>2|/ja/about|/ja/about?page=3</p></body></html>");
    }

    [Fact]
    public async Task The_Preview_Should_Describe_The_Page_It_Rendered()
    {
        var fixture = new Fixture(PageMatch("Default.cshtml"));

        var preview = await fixture.RenderAsync(Theme("<p>{{ page.name }}</p>"));

        preview.PageName.ShouldBe("about");
        preview.CultureName.ShouldBe("ja");
        preview.PageTemplate.ShouldBe("Default");
        preview.Html.ShouldEndWith("<p>about</p></body></html>");
    }

    [Fact]
    public async Task The_Request_Should_Keep_No_Head_Metadata_Of_The_Previewed_Page()
    {
        var fixture = new Fixture(PageMatch());

        await fixture.RenderAsync(Theme("<p>x</p>"));

        fixture.Scriban.Host.LanguageContext.HeadMetadata.ShouldBeNull();
        fixture.Scriban.Host.LanguageContext.RenderedPath.ShouldBeNull();
    }

    [Fact]
    public async Task A_Preview_Should_Include_The_Snapshots_Templates()
    {
        var fixture = new Fixture(PageMatch());

        var preview = await fixture.RenderAsync(Theme(
            "<main>{{ include \"partials/card\" }}</main>",
            new() { ["partials/card"] = "<div class=\"card\">{{ page.name }}</div>" }));

        preview.Html.ShouldEndWith("<main><div class=\"card\">about</div></main></body></html>");
    }

    /// <summary>The output bound is not a Scriban exception; a preview still reports it as the template's failure.</summary>
    [Fact]
    public async Task Output_Beyond_The_Limit_Should_Fail_With_Render_Failed()
    {
        var fixture = new Fixture(PageMatch(), new SiteScribanOptions { MaxOutputLength = 100 });

        var exception = await Should.ThrowAsync<BusinessException>(
            () => fixture.RenderAsync(Theme("{{ for i in 1..50 }}xxxxxxxxxx{{ end }}")));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewRenderFailed);
        exception.Data["Message"].ShouldBeOfType<string>().ShouldContain("100");
    }

    /// <summary>A working copy may be saved with errors; previewing it reports them rather than failing obscurely.</summary>
    [Fact]
    public async Task A_Template_That_Does_Not_Parse_Should_Fail_With_Render_Failed()
    {
        var fixture = new Fixture(PageMatch());

        var exception = await Should.ThrowAsync<BusinessException>(() => fixture.RenderAsync(Theme("{{ if x }}no end")));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewRenderFailed);
        exception.Data["Message"].ShouldBeOfType<string>().ShouldContain("'Default' does not parse");
    }

    /// <summary>
    /// The engine's own exception would reach an HTTP or MCP caller as "an internal error"; the preview hands on
    /// its message under a business code instead.
    /// </summary>
    [Fact]
    public async Task A_Template_That_Fails_At_Run_Time_Should_Fail_With_Render_Failed()
    {
        var fixture = new Fixture(PageMatch());

        var exception = await Should.ThrowAsync<BusinessException>(() => fixture.RenderAsync(Theme("\n{{ typo_var }}")));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewRenderFailed);
        exception.Data["Name"].ShouldBe("Default");
        exception.Data["Message"].ShouldBeOfType<string>().ShouldContain("typo_var");
        exception.InnerException.ShouldBeAssignableTo<ScriptRuntimeException>();

        fixture.Scriban.Host.LanguageContext.RenderedPath.ShouldBeNull();
    }

    /// <summary>
    /// The request is the admin API's, but the page is <c>/ja/about</c>: its links are in Japanese, as on the
    /// live page.
    /// </summary>
    [Fact]
    public async Task Url_Should_Follow_The_Previewed_Pages_Language()
    {
        var fixture = new Fixture(PageMatch());

        var preview = await fixture.RenderAsync(Theme("[{{ url \"~/contact\" }}]"), "/ja/about");

        preview.Html.ShouldEndWith("[/ja/contact]</body></html>");
    }

    private class FakeRoutingAppService(RouteMatchDto match) : IRoutingPublicAppService
    {
        public List<string> RequestedPaths { get; } = new();

        public Task<RouteMatchDto> ResolveAsync(ResolvePathInput input)
        {
            RequestedPaths.Add(input.Path);
            return Task.FromResult(match);
        }
    }

    private class FakeHeadMetadataAppService(HeadMetadataDto headMetadata) : IHeadMetadataPublicAppService
    {
        public List<string> RequestedPaths { get; } = new();

        public Task<HeadMetadataDto?> ResolveAsync(ResolveHeadMetadataInput input)
        {
            RequestedPaths.Add(input.Path);
            return Task.FromResult<HeadMetadataDto?>(headMetadata);
        }
    }

    private class FakeFieldAppService : IFieldPublicAppService
    {
        public Task<FieldDto> GetAsync(Guid id) => throw new NotSupportedException();

        public Task<ListResultDto<FieldDto>> GetListAsync(IEnumerable<Guid> ids) => throw new NotSupportedException();
    }
}
