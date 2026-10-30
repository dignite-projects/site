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
using Dignite.Site.Public.Templates;
using Dignite.Site.Templates;
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
        FakeTemplateAppService? templateAppService = null)
    {
        var controller = new SiteRenderController(
            new FakeRoutingAppService(match), new FakeFieldAppService(), new FakeHeadMetadataAppService(HeadMetadata),
            templateAppService ?? new FakeTemplateAppService(new SiteTemplateRenderSetDto()))
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

    /// <summary>
    /// A page whose template the tenant has published in Scriban renders through it - in the page's language -
    /// instead of the Razor view of the same name.
    /// </summary>
    [Fact]
    public async Task A_Published_Template_Should_Render_With_Scriban()
    {
        var scriban = new ScribanTestRenderer("en,ja", "/ja/about");
        var templates = new FakeTemplateAppService(ScribanTestRenderer.RenderSet(
            "<h1>{{ page.name }}</h1>",
            "<html lang=\"{{ culture }}\"><head>{{ site_head }}</head><body>{{ body }}</body></html>"));

        var result = await RenderAsync(scriban.Host, PageMatch("en", "ja"), templates);

        templates.RequestedNames.ShouldBe(new[] { "Default" });
        var scoped = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>();
        scoped.CultureName.ShouldBe("ja");
        scoped.Inner.ShouldBeOfType<ScribanPageResult>().TemplateName.ShouldBe("Default");

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

    /// <summary>No published template of that name: the Razor view, exactly as before.</summary>
    [Fact]
    public async Task No_Published_Template_Should_Keep_The_Razor_View()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        var templates = new FakeTemplateAppService(new SiteTemplateRenderSetDto
        {
            // A published layout alone does not switch a page over - only its own template does.
            Layout = ScribanTestRenderer.Version("{{ site_head }}{{ body }}")
        });
        var match = PageMatch("en", "ja");
        match.Page!.Template = "Default.cshtml";

        var result = await RenderAsync(host, match, templates);

        templates.RequestedNames.ShouldBe(new[] { "Default" });
        var view = result.ShouldBeOfType<SiteRenderController.CultureScopedResult>().Inner.ShouldBeOfType<ViewResult>();
        view.ViewName.ShouldBe("Default");
        view.Model.ShouldBeOfType<SiteRenderViewModel>().Page.Name.ShouldBe("about");
    }

    /// <summary>A 404 never asks for templates.</summary>
    [Fact]
    public async Task An_Unmatched_Path_Should_Not_Look_Up_A_Template()
    {
        var host = new SiteLanguageTestHost("en", "/missing");
        var templates = new FakeTemplateAppService(new SiteTemplateRenderSetDto());

        var result = await RenderAsync(host, new RouteMatchDto { Matched = false }, templates);

        result.ShouldBeOfType<NotFoundResult>();
        templates.RequestedNames.ShouldBeEmpty();
    }

    private class FakeTemplateAppService(SiteTemplateRenderSetDto renderSet) : ISiteTemplatePublicAppService
    {
        public List<string> RequestedNames { get; } = new();

        public Task<SiteTemplateRenderSetDto> GetRenderSetAsync(string name)
        {
            RequestedNames.Add(name);
            return Task.FromResult(renderSet);
        }

        /// <summary>The asset endpoint's lookup; rendering a page never makes it.</summary>
        public Task<SiteTemplateVersionDto?> FindPublishedAsync(string name) => throw new NotSupportedException();
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
