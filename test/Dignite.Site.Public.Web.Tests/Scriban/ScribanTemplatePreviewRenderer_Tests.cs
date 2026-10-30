using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.Templates;
using Dignite.Site.Templates;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// <see cref="ScribanTemplatePreviewRenderer"/> over a real <see cref="ScribanPageRenderer"/>
/// (<see cref="ScribanTestRenderer"/>), with the Public application services it calls faked: the page a path
/// resolves to, rendered with the previewed version in place of the published one - and nothing else changed.
/// The request is the admin API call that asked for the preview, not the page.
/// </summary>
public class ScribanTemplatePreviewRenderer_Tests
{
    private const string PublishedLayout =
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

    private static SiteTemplateVersionDto Version(string body, int number = 3) => new()
    {
        Id = Guid.NewGuid(),
        TemplateId = Guid.NewGuid(),
        Number = number,
        Body = body
    };

    private sealed class Fixture
    {
        public Fixture(RouteMatchDto match, SiteTemplateRenderSetDto published, SiteScribanOptions? options = null)
        {
            Scriban = new ScribanTestRenderer("en,ja", "/api/site-admin/templates/preview", options);
            Routing = new FakeRoutingAppService(match);
            HeadMetadata = new FakeHeadMetadataAppService(ScribanTestRenderer.HeadMetadata);
            Templates = new FakeTemplateAppService(published);

            Renderer = new ScribanTemplatePreviewRenderer(
                Routing,
                new FakeFieldAppService(),
                HeadMetadata,
                Templates,
                Scriban.Renderer,
                Scriban.Host.LanguageContext,
                new HttpContextAccessor { HttpContext = Scriban.Host.HttpContext });
        }

        public ScribanTestRenderer Scriban { get; }

        public FakeRoutingAppService Routing { get; }

        public FakeHeadMetadataAppService HeadMetadata { get; }

        public FakeTemplateAppService Templates { get; }

        public ScribanTemplatePreviewRenderer Renderer { get; }

        public Task<SiteTemplatePreviewDto> RenderAsync(string templateName, SiteTemplateVersionDto version, string path = "/ja/about")
        {
            return Renderer.RenderAsync(new SiteTemplatePreviewRequest
            {
                TemplateName = templateName,
                Version = version,
                Path = path
            });
        }
    }

    [Fact]
    public async Task A_Page_Template_Version_Should_Render_Inside_The_Published_Layout()
    {
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto
        {
            Template = Version("<h1>published</h1>", 2),
            Layout = Version(PublishedLayout, 1)
        });

        var preview = await fixture.RenderAsync("Default", Version("<h1>preview {{ page.name }}</h1>"));

        fixture.Templates.RequestedNames.ShouldBe(new[] { "Default" });
        preview.Html.ShouldStartWith("<html lang=\"ja\"><head><link href=\"https://acme.example/ja/about\" rel=\"canonical\">");
        preview.Html.ShouldEndWith("<body><h1>preview about</h1></body></html>");
        preview.Html.ShouldNotContain("<h1>published</h1>");
    }

    [Fact]
    public async Task A_Layout_Version_Should_Wrap_The_Published_Page_Template()
    {
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto
        {
            Template = Version("<h1>published {{ page.name }}</h1>", 2),
            Layout = Version("<div class=\"old\">{{ site_head }}{{ body }}</div>", 1)
        });

        var preview = await fixture.RenderAsync(
            SiteTemplateConsts.LayoutName,
            Version("<main class=\"new\">{{ site_head }}<section>{{ body }}</section></main>"));

        // The page's own template is what the layout wraps, so that is the name looked up.
        fixture.Templates.RequestedNames.ShouldBe(new[] { "Default" });
        preview.Html.ShouldStartWith("<main class=\"new\">");
        preview.Html.ShouldEndWith("<section><h1>published about</h1></section></main>");
        preview.Html.ShouldNotContain("class=\"old\"");
    }

    [Fact]
    public async Task A_Layout_Version_Should_Wrap_An_Empty_Body_When_The_Page_Has_No_Published_Template()
    {
        // The page is still rendered by its Razor view, which cannot be rendered into a Scriban layout.
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto
        {
            Layout = Version("<div class=\"old\">{{ site_head }}{{ body }}</div>", 1)
        });

        var preview = await fixture.RenderAsync(
            SiteTemplateConsts.LayoutName,
            Version("<main>{{ site_head }}<section>{{ body }}</section></main>"));

        preview.Html.ShouldStartWith("<main>");
        preview.Html.ShouldEndWith("<section></section></main>");

        // Twice, through the same template cache: the stand-in empty template stays empty.
        var again = await fixture.RenderAsync(
            SiteTemplateConsts.LayoutName,
            Version("<article>{{ site_head }}[{{ body }}]</article>"));

        again.Html.ShouldEndWith("[]</article>");
    }

    [Fact]
    public async Task A_Path_No_Page_Answers_Should_Throw_Path_Not_Found()
    {
        var fixture = new Fixture(new RouteMatchDto { Matched = false, CultureName = "en" }, new SiteTemplateRenderSetDto());

        var exception = await Should.ThrowAsync<BusinessException>(
            () => fixture.RenderAsync("Default", Version("{{ site_head }}"), "/missing"));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewPathNotFound);
        exception.Data["Path"].ShouldBe("/missing");
        fixture.Templates.RequestedNames.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ja/about", "/ja/about")]
    [InlineData(" /ja/about ", "/ja/about")]
    [InlineData("", "/")]
    public async Task The_Path_Should_Be_Normalized(string path, string expected)
    {
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto());

        var preview = await fixture.RenderAsync("Default", Version("{{ site_head }}<p>{{ page.name }}</p>"), path);

        fixture.Routing.RequestedPaths.ShouldBe(new[] { expected });
        fixture.HeadMetadata.RequestedPaths.ShouldBe(new[] { expected });
        preview.Path.ShouldBe(expected);
    }

    [Fact]
    public async Task The_Preview_Should_Describe_The_Page_It_Rendered()
    {
        var fixture = new Fixture(PageMatch("Default.cshtml"), new SiteTemplateRenderSetDto());

        var preview = await fixture.RenderAsync("Blog/Article", Version("{{ site_head }}<p>{{ page.name }}</p>", 7));

        // The page's template value as it is, while the render set is looked up by the bare view name.
        fixture.Templates.RequestedNames.ShouldBe(new[] { "Default" });
        preview.PageName.ShouldBe("about");
        preview.CultureName.ShouldBe("ja");
        preview.PageTemplate.ShouldBe("Default.cshtml");
        preview.VersionNumber.ShouldBe(7);
        preview.Html.ShouldEndWith("<p>about</p>");
    }

    [Fact]
    public async Task The_Request_Should_Keep_No_Head_Metadata_Of_The_Previewed_Page()
    {
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto());

        await fixture.RenderAsync("Default", Version("{{ site_head }}"));

        fixture.Scriban.Host.LanguageContext.HeadMetadata.ShouldBeNull();
        fixture.Scriban.Host.LanguageContext.RenderedPath.ShouldBeNull();
    }

    /// <summary>The output bound is not a Scriban exception; a preview still reports it as the template's failure.</summary>
    [Fact]
    public async Task Output_Beyond_The_Limit_Should_Fail_With_Render_Failed()
    {
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto(), new SiteScribanOptions { MaxOutputLength = 100 });

        var exception = await Should.ThrowAsync<BusinessException>(
            () => fixture.RenderAsync("Default", Version("{{ site_head }}{{ for i in 1..50 }}xxxxxxxxxx{{ end }}")));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewRenderFailed);
        exception.Data["Message"].ShouldBeOfType<string>().ShouldContain("100");
    }

    /// <summary>
    /// The engine's own exception would reach an HTTP or MCP caller as "an internal error"; the preview hands on
    /// its message under a business code instead.
    /// </summary>
    [Fact]
    public async Task A_Template_That_Fails_At_Run_Time_Should_Fail_With_Render_Failed()
    {
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto());

        var exception = await Should.ThrowAsync<BusinessException>(
            () => fixture.RenderAsync("Default", Version("{{ site_head }}\n{{ typo_var }}")));

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
        var fixture = new Fixture(PageMatch(), new SiteTemplateRenderSetDto());

        var preview = await fixture.RenderAsync("Default", Version("{{ site_head }}[{{ url \"~/contact\" }}]"), "/ja/about");

        preview.Html.ShouldEndWith("[/ja/contact]");
    }

    private class FakeTemplateAppService(SiteTemplateRenderSetDto renderSet) : ISiteTemplatePublicAppService
    {
        public List<string> RequestedNames { get; } = new();

        public Task<SiteTemplateRenderSetDto> GetRenderSetAsync(string name)
        {
            RequestedNames.Add(name);
            return Task.FromResult(renderSet);
        }
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
