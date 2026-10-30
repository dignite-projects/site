using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// Every value a template writes is HTML-encoded, as in Razor; only the platform's own HTML - <c>site_head</c>,
/// <c>render_field</c>, the layout's <c>body</c> - and what a template passes through <c>html_raw</c> is not.
/// </summary>
public class ScribanHtmlEncoding_Tests
{
    private const string HeadOnlyLayout = "{{ site_head }}{{ body }}";

    /// <summary>A route's filter values end up in the title, straight from the URL: a reflected XSS if written raw.</summary>
    [Fact]
    public async Task A_Title_Carrying_Markup_From_The_Url_Should_Be_Encoded()
    {
        var viewModel = new SiteRenderViewModel
        {
            Page = ScribanTestRenderer.Page(),
            CultureName = "ja",
            FieldFilters = new Dictionary<string, string>(),
            HeadMetadata = new HeadMetadataDto
            {
                MetaTitle = "Blog - <script>alert(1)</script>",
                BaseMetaTitle = "Blog",
                CanonicalUrl = "https://acme.example/ja/blog"
            }
        };

        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ site_head }}<title>{{ head.meta_title }}</title>", viewModel: viewModel);

        html.ShouldContain("<title>Blog - &lt;script&gt;alert(1)&lt;/script&gt;</title>");
        html.ShouldNotContain("<script>");
    }

    [Fact]
    public async Task A_Content_Field_Carrying_Markup_Should_Be_Encoded()
    {
        var viewModel = new SiteRenderViewModel
        {
            Page = ScribanTestRenderer.Page(),
            CultureName = "ja",
            FieldFilters = new Dictionary<string, string>(),
            HeadMetadata = ScribanTestRenderer.HeadMetadata,
            Content = ScribanTestRenderer.Content("trip", "<i>Trip</i> & more")
        };

        var html = await new ScribanTestRenderer().RenderAsync(
            "[{{ content.fields.Title }}][{{ field_list[0].value }}]", HeadOnlyLayout, viewModel);

        html.ShouldEndWith("[&lt;i&gt;Trip&lt;/i&gt; &amp; more][&lt;i&gt;Trip&lt;/i&gt; &amp; more]");
    }

    [Fact]
    public async Task The_Layouts_Body_Should_Be_Written_As_It_Is()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "<p>{{ \"R&D\" }}</p>", "<main>{{ body }}</main>{{ site_head }}");

        // Encoded once, by the page template - not again by the layout.
        html.ShouldStartWith("<main><p>R&amp;D</p></main>");
    }

    [Fact]
    public async Task Render_Field_Should_Be_Written_As_It_Is()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "[{{ render_field \"Title\" }}]", HeadOnlyLayout, ScribanTestRenderer.ContentViewModel());

        html.ShouldEndWith("[<div class=\"partial\">FlexFields/Text</div>]");
    }

    [Fact]
    public async Task Site_Head_Should_Be_Written_As_It_Is()
    {
        var html = await new ScribanTestRenderer().RenderAsync("{{ site_head }}");

        html.ShouldStartWith("<link href=\"https://acme.example/ja/about\" rel=\"canonical\">");
    }

    [Fact]
    public async Task Html_Raw_Should_Write_A_Value_As_It_Is()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "[{{ \"<b>x</b>\" | html_raw }}][{{ html_raw \"<i>y</i>\" }}][{{ \"<b>x</b>\" }}][{{ t \"Emphasis\" }}][{{ t \"Emphasis\" | html_raw }}]",
            HeadOnlyLayout);

        html.ShouldEndWith("[<b>x</b>][<i>y</i>][&lt;b&gt;x&lt;/b&gt;][&lt;em&gt;Read&lt;/em&gt;][<em>Read</em>]");
    }

    [Fact]
    public async Task A_Raw_Value_Through_A_String_Function_Should_Be_Encoded_Again()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "[{{ \"<b>x</b>\" | html_raw | string.upcase }}]", HeadOnlyLayout);

        html.ShouldEndWith("[&lt;B&gt;X&lt;/B&gt;]");
    }
}
