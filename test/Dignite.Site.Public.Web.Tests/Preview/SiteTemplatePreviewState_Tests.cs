using System;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Scriban;
using Dignite.Site.Public.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// One request in draft mode: the cookie is honored only on its tenant, and the version it names goes into the slot
/// its name says - in a new render set, the published one untouched.
/// </summary>
public class SiteTemplatePreviewState_Tests
{
    private static readonly Guid TenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private sealed class Fixture
    {
        public Fixture(string path = "/ja/about", string pathBase = "")
        {
            Host = new SiteLanguageTestHost("en,ja", path, pathBase);
            Preview = new SiteTemplatePreviewTestContext(Host);
        }

        public SiteLanguageTestHost Host { get; }

        public SiteTemplatePreviewTestContext Preview { get; }
    }

    /// <summary>A page template "Default", the layout, a partial and an asset, all published.</summary>
    private static SiteTemplateRenderSetDto Published()
    {
        var published = ScribanTestRenderer.RenderSet("<h1>published</h1>", "{{ site_head }}{{ body }}");
        published.Partials["partials/card"] = ScribanTestRenderer.Version("<div>card</div>");
        published.Assets["assets/site.css"] = Guid.NewGuid();
        return published;
    }

    [Fact]
    public async Task Without_A_Cookie_Nothing_Should_Change()
    {
        var fixture = new Fixture();
        var state = fixture.Preview.CreateState();
        var published = Published();

        state.Ticket.ShouldBeNull();
        (await state.ApplyAsync("Default", published)).ShouldBeSameAs(published);
        (await state.GetOverrideVersionAsync()).ShouldBeNull();
        state.GetBannerHtml(new ActionContext(fixture.Host.HttpContext, new RouteData(), new ActionDescriptor())).ShouldBeNull();
        fixture.Preview.Versions.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Another_Tenants_Cookie_Should_Be_Ignored()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("Default", 3, TenantId);
        fixture.Preview.Versions.Add("Default", 3, "<h1>draft</h1>");
        var state = fixture.Preview.CreateState();
        var published = Published();

        // The request is the host's.
        state.Ticket.ShouldBeNull();
        (await state.ApplyAsync("Default", published)).ShouldBeSameAs(published);
        fixture.Preview.Versions.Requests.ShouldBeEmpty();

        using (fixture.Host.CurrentTenant.Change(TenantId))
        {
            state.Ticket.ShouldNotBeNull().TemplateName.ShouldBe("Default");
        }
    }

    [Fact]
    public void A_Cookie_That_Does_Not_Verify_Should_Be_Ignored()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("CfDJ8-not-a-ticket");

        fixture.Preview.CreateState().Ticket.ShouldBeNull();
    }

    [Fact]
    public async Task A_Page_Template_Should_Replace_The_Pages_Own_Template()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("Default", 3);
        var draft = fixture.Preview.Versions.Add("Default", 3, "<h1>draft</h1>");
        var published = Published();
        var publishedTemplate = published.Template;

        var renderSet = await fixture.Preview.CreateState().ApplyAsync("Default", published);

        renderSet.ShouldNotBeSameAs(published);
        renderSet.Template.ShouldBeSameAs(draft);
        renderSet.Layout.ShouldBeSameAs(published.Layout);
        published.Template.ShouldBeSameAs(publishedTemplate);
        fixture.Preview.Versions.Requests.ShouldBe(new[] { ("Default", 3) });
    }

    /// <summary>The live site substitutes a page template only on the pages that use it.</summary>
    [Fact]
    public async Task A_Page_Template_Of_Another_Name_Should_Leave_The_Page_As_It_Is()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("Blog/Article", 3);
        fixture.Preview.Versions.Add("Blog/Article", 3, "<h1>draft</h1>");
        var published = Published();

        var renderSet = await fixture.Preview.CreateState().ApplyAsync("Default", published);

        renderSet.Template.ShouldBeSameAs(published.Template);
        renderSet.Layout.ShouldBeSameAs(published.Layout);
        renderSet.Partials.Count.ShouldBe(published.Partials.Count);
        renderSet.Partials["partials/card"].ShouldBeSameAs(published.Partials["partials/card"]);
        renderSet.Assets["assets/site.css"].ShouldBe(published.Assets["assets/site.css"]);
    }

    [Fact]
    public async Task The_Layout_Should_Replace_The_Published_Layout()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("layout", 2);
        var draft = fixture.Preview.Versions.Add("layout", 2, "{{ site_head }}<main>{{ body }}</main>");
        var published = Published();
        var publishedLayout = published.Layout;

        var renderSet = await fixture.Preview.CreateState().ApplyAsync("Default", published);

        renderSet.Layout.ShouldBeSameAs(draft);
        renderSet.Template.ShouldBeSameAs(published.Template);
        published.Layout.ShouldBeSameAs(publishedLayout);
    }

    /// <summary>A Razor page stays Razor: the layout alone does not switch it over.</summary>
    [Fact]
    public async Task The_Layout_Should_Not_Give_A_Razor_Page_A_Template()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("layout", 2);
        fixture.Preview.Versions.Add("layout", 2, "{{ site_head }}{{ body }}");

        var renderSet = await fixture.Preview.CreateState().ApplyAsync("Default", new SiteTemplateRenderSetDto());

        renderSet.Template.ShouldBeNull();
        renderSet.Layout.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Partial_Should_Take_Its_Slot_Without_Touching_The_Published_Set()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("partials/card", 7);
        var draft = fixture.Preview.Versions.Add("partials/card", 7, "<div>draft card</div>");
        var published = Published();
        var publishedCard = published.Partials["partials/card"];

        var renderSet = await fixture.Preview.CreateState().ApplyAsync("Default", published);

        renderSet.Partials["partials/card"].ShouldBeSameAs(draft);
        renderSet.Template.ShouldBeSameAs(published.Template);
        published.Partials["partials/card"].ShouldBeSameAs(publishedCard);
        renderSet.Partials.ShouldNotBeSameAs(published.Partials);
    }

    /// <summary>A partial that was never published is there for <c>include</c> all the same.</summary>
    [Fact]
    public async Task An_Unpublished_Partial_Should_Be_Added()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("partials/new", 1);
        var draft = fixture.Preview.Versions.Add("partials/new", 1, "<p>new</p>");
        var published = Published();

        var renderSet = await fixture.Preview.CreateState().ApplyAsync("Default", published);

        renderSet.Partials["partials/new"].ShouldBeSameAs(draft);
        published.Partials.ContainsKey("partials/new").ShouldBeFalse();
    }

    [Fact]
    public async Task An_Asset_Should_Take_Its_Slot_Without_Touching_The_Published_Set()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("assets/site.css", 4);
        var draft = fixture.Preview.Versions.Add("assets/site.css", 4, "body { color: red; }");
        var published = Published();
        var publishedId = published.Assets["assets/site.css"];

        var renderSet = await fixture.Preview.CreateState().ApplyAsync("Default", published);

        renderSet.Assets["assets/site.css"].ShouldBe(draft.Id);
        published.Assets["assets/site.css"].ShouldBe(publishedId);
    }

    /// <summary>A version deleted since the link was issued: the published site, quietly.</summary>
    [Fact]
    public async Task A_Version_That_Is_Gone_Should_Change_Nothing()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("Default", 9);
        var published = Published();

        var state = fixture.Preview.CreateState();

        state.Ticket.ShouldNotBeNull();
        (await state.ApplyAsync("Default", published)).ShouldBeSameAs(published);
    }

    /// <summary>A host rendering against a remote Public API has no provider: draft mode is off.</summary>
    [Fact]
    public async Task Without_A_Version_Provider_Nothing_Should_Change()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("Default", 3);
        fixture.Preview.Versions.Add("Default", 3, "<h1>draft</h1>");
        fixture.Preview.HasVersionProvider = false;
        var published = Published();

        (await fixture.Preview.CreateState().ApplyAsync("Default", published)).ShouldBeSameAs(published);
    }

    [Fact]
    public async Task The_Version_Should_Be_Looked_Up_Once_Per_Request()
    {
        var fixture = new Fixture();
        fixture.Preview.SetCookie("assets/site.css", 4);
        fixture.Preview.Versions.Add("assets/site.css", 4, "body {}");
        var state = fixture.Preview.CreateState();

        await state.ApplyAsync("Default", Published());
        await state.GetOverrideVersionAsync();

        fixture.Preview.Versions.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public void An_Activated_Ticket_Should_Be_Honored_Without_A_Cookie()
    {
        var fixture = new Fixture();
        var state = fixture.Preview.CreateState();

        state.Activate(new SiteTemplatePreviewTicket(null, "Default", 3));

        state.Ticket.ShouldNotBeNull().VersionNumber.ShouldBe(3);
    }

    [Fact]
    public void The_Banner_Should_Name_The_Version_And_Exit_Back_To_This_Address()
    {
        var fixture = new Fixture("/ja/about", pathBase: "/site");
        fixture.Host.HttpContext.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?x=1");
        fixture.Preview.SetCookie("partials/card", 7);
        var state = fixture.Preview.CreateState();

        var banner = state.GetBannerHtml(new ActionContext(fixture.Host.HttpContext, new RouteData(), new ActionDescriptor()));

        banner.ShouldNotBeNull();
        banner.ShouldStartWith("<div id=\"" + SiteTemplatePreviewConsts.BannerElementId + "\"");
        banner.ShouldContain("<span>Previewing partials/card v7</span>");
        banner.ShouldContain("href=\"/site/site-preview/exit?returnUrl=%2Fsite%2Fja%2Fabout%3Fx%3D1\"");
        banner.ShouldContain(">Exit preview</a>");
    }

    [Fact]
    public void The_Response_Headers_Should_Keep_The_Preview_Out_Of_Caches_And_Indexes()
    {
        var fixture = new Fixture();

        fixture.Preview.CreateState().ApplyResponseHeaders(fixture.Host.HttpContext.Response);

        fixture.Host.HttpContext.Response.Headers["X-Robots-Tag"].ToString().ShouldBe("noindex");
        fixture.Host.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }
}
