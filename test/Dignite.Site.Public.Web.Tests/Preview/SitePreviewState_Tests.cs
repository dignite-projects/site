using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dignite.Site.Localization;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Scriban;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// One request of a preview: whether the cookie is honored, which theme it reads and how often, what the banner says,
/// and the page a failing template turns into.
/// </summary>
public class SitePreviewState_Tests
{
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static SiteThemeSnapshotDto Snapshot(int? releaseNumber = null)
    {
        var snapshot = ScribanTestRenderer.Snapshot(new Dictionary<string, string> { ["Default"] = "<h1>x</h1>" });
        snapshot.ReleaseNumber = releaseNumber;
        return snapshot;
    }

    private static ActionContext ActionContext(SiteLanguageTestHost host) => new(host.HttpContext, new RouteData(), new ActionDescriptor());

    [Fact]
    public async Task Without_A_Cookie_Nothing_Should_Be_Previewed_Or_Read()
    {
        var host = new SiteLanguageTestHost("en", "/about");
        host.Preview.Source.WorkingCopy = Snapshot();
        var state = host.Preview.CreateState();

        state.Ticket.ShouldBeNull();
        state.IsAvailable.ShouldBeFalse();
        (await state.GetRenderSetAsync()).ShouldBeNull();
        (await state.FindAssetAsync("assets/site.css")).ShouldBeNull();
        state.GetBannerHtml(ActionContext(host)).ShouldBeNull();
        host.Preview.Source.SnapshotRequests.ShouldBeEmpty();
        host.Preview.Source.AssetRequests.ShouldBeEmpty();
    }

    /// <summary>Read once per request, however often the page asks - and again on the next request, so a save shows.</summary>
    [Fact]
    public async Task The_Working_Copy_Should_Be_Read_Once_Per_Request()
    {
        var host = new SiteLanguageTestHost("en", "/about");
        host.Preview.Source.WorkingCopy = Snapshot();
        host.Preview.SetCookie();
        var state = host.Preview.CreateState();

        var first = await state.GetRenderSetAsync();
        var second = await state.GetRenderSetAsync();

        first.ShouldNotBeNull();
        second.ShouldBeSameAs(first);
        first.FindTemplate("Default").ShouldNotBeNull();
        host.Preview.Source.SnapshotRequests.ShouldBe(new Guid?[] { null });

        await host.Preview.CreateState().GetRenderSetAsync();
        host.Preview.Source.SnapshotRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_Cookie_Should_Be_Honored_On_Its_Own_Tenant_Only()
    {
        var host = new SiteLanguageTestHost("en", "/about");
        host.Preview.Source.WorkingCopy = Snapshot();
        host.Preview.SetCookie(tenantId: TenantId);

        host.Preview.CreateState().Ticket.ShouldBeNull();

        using (host.CurrentTenant.Change(TenantId))
        {
            var state = host.Preview.CreateState();
            state.Ticket.ShouldNotBeNull();
            (await state.GetRenderSetAsync()).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Without_A_Source_The_Preview_Should_Be_Off()
    {
        var host = new SiteLanguageTestHost("en", "/about");
        host.Preview.HasSource = false;
        host.Preview.SetCookie();
        var state = host.Preview.CreateState();

        state.Ticket.ShouldNotBeNull();
        state.IsAvailable.ShouldBeFalse();
        (await state.GetRenderSetAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task The_Banner_Should_Say_What_Is_Shown_And_Lead_Back_Here()
    {
        var host = new SiteLanguageTestHost("en", "/ja/about", pathBase: "/site");
        host.HttpContext.Request.QueryString = new QueryString("?page=2");
        var releaseId = Guid.NewGuid();
        host.Preview.Source.Releases[releaseId] = Snapshot(releaseNumber: 4);
        host.Preview.SetCookie(releaseId);
        var state = host.Preview.CreateState();

        await state.GetRenderSetAsync();
        var banner = state.GetBannerHtml(ActionContext(host));

        banner.ShouldNotBeNull();
        banner.ShouldContain("<span>Previewing release #4</span>");
        banner.ShouldContain("href=\"/site/site-preview/exit?returnUrl=%2Fsite%2Fja%2Fabout%3Fpage%3D2\"");
        banner.ShouldContain(">Exit preview</a>");
    }

    /// <summary>A preview renders a failing template as a page that says what failed, under the banner.</summary>
    [Fact]
    public async Task A_Failing_Template_Should_Be_Shown_In_A_Preview()
    {
        var scriban = new ScribanTestRenderer();
        var renderSet = ScribanTestRenderer.RenderSet(new Dictionary<string, string> { ["Default"] = "<h1>x</h1>" }, "layouts/missing");
        scriban.Host.HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(scriban.Renderer)
            .AddSingleton<IStringLocalizer<SiteResource>>(new FakeSiteLocalizer())
            .BuildServiceProvider();
        var body = new MemoryStream();
        scriban.Host.HttpContext.Response.Body = body;

        var result = new ScribanPageResult("Default", renderSet, ScribanTestRenderer.PageViewModel())
        {
            Banner = "<div id=\"banner\"></div>",
            ShowsErrors = true
        };
        await result.ExecuteResultAsync(scriban.ActionContext);

        scriban.Host.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        var html = Encoding.UTF8.GetString(body.ToArray());
        html.ShouldContain("<div id=\"banner\"></div>");
        html.ShouldContain("<h1 style=\"font-size:20px\">Rendering Default failed</h1>");
        html.ShouldContain("The layout &#x27;layouts/missing&#x27;");
    }

    /// <summary>Outside a preview a failing template is the host's to handle, as before.</summary>
    [Fact]
    public async Task A_Failing_Template_Should_Throw_Outside_A_Preview()
    {
        var scriban = new ScribanTestRenderer();
        var renderSet = ScribanTestRenderer.RenderSet(new Dictionary<string, string> { ["Default"] = "<h1>x</h1>" }, "layouts/missing");
        scriban.Host.HttpContext.RequestServices = new ServiceCollection().AddSingleton(scriban.Renderer).BuildServiceProvider();

        var result = new ScribanPageResult("Default", renderSet, ScribanTestRenderer.PageViewModel());

        await Should.ThrowAsync<InvalidOperationException>(() => result.ExecuteResultAsync(scriban.ActionContext));
    }
}
