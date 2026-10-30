using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// The page template and layout the user documentation shows, verbatim: if one of these fails, the documented
/// example no longer works. Keep them identical to the docs.
/// </summary>
public class ScribanDocumentedTemplates_Tests
{
    public const string PageTemplate = """
        <!DOCTYPE html>
        <html lang="{{ culture }}">
        <head>
            <meta charset="utf-8" />
            <title>{{ head?.meta_title ?? page.display_name }}</title>
            {{ site_head }}
        </head>
        <body>
        <h1>{{ page.display_name }}</h1>
        {{ if content }}
            <p>{{ content.publish_time | date.to_string "%Y-%m-%d" }}</p>
            <hr />
            {{ for field in field_list }}
            <section>
                <h2>{{ field.display_name }}</h2>
                {{ render_field field.name }}
            </section>
            {{ end }}
        {{ else }}
            {{ for key in (object.keys field_filters) }}<p>{{ key }}: {{ field_filters[key] }}</p>{{ end }}
            {{ list = contents { max: 20 } }}
            <ul>
            {{ for item in list.items }}
                <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
            {{ end }}
            </ul>
            {{ if list.total_count > list.items.size }}<p>{{ list.items.size }} / {{ list.total_count }}</p>{{ end }}
        {{ end }}
        </body>
        </html>
        """;

    public const string Layout = """
        <!DOCTYPE html>
        <html lang="{{ culture }}">
        <head>
            <meta charset="utf-8" />
            <title>{{ head?.meta_title ?? page.display_name }}</title>
            {{ site_head }}
        </head>
        <body>
        <nav>{{ for lang in languages }}<a href="{{ lang.url }}">{{ lang.culture_name }}</a> {{ end }}</nav>
        {{ body }}
        </body>
        </html>
        """;

    [Fact]
    public async Task The_Page_Template_Should_Render_A_Content()
    {
        var renderer = new ScribanTestRenderer();

        var html = await renderer.RenderAsync(PageTemplate, viewModel: ScribanTestRenderer.ContentViewModel());

        html.ShouldStartWith("<!DOCTYPE html>");
        html.ShouldContain("<html lang=\"ja\">");
        html.ShouldContain("<title>About</title>");
        html.ShouldContain("<link href=\"https://acme.example/ja/about\" rel=\"canonical\">");
        html.ShouldContain("<h1>About us</h1>");
        html.ShouldContain("<p>2026-08-15</p>");
        html.ShouldContain("<h2>Title</h2>");
        html.ShouldContain("<div class=\"partial\">FlexFields/Text</div>");
        html.ShouldContain("<h2>Gallery</h2>");
        html.ShouldContain("<div class=\"partial\">FlexFields/FileExplorer</div>");
        html.ShouldNotContain("<ul>");
        html.TrimEnd().ShouldEndWith("</html>");

        renderer.PartialRenderer.Calls.Count.ShouldBe(3);
        renderer.ContentListQuery.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Page_Template_Should_Render_A_Filtered_List()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.Items.Add(ScribanTestRenderer.Content("first", "First post"));
        renderer.ContentListQuery.TotalCount = 7;
        var viewModel = ScribanTestRenderer.PageViewModel(); // one field filter: category = engineering

        var html = await renderer.RenderAsync(PageTemplate, viewModel: viewModel);

        html.ShouldContain("<title>About</title>");
        html.ShouldContain("<p>category: engineering</p>");
        html.ShouldContain("<li><a href=\"/ja/about/first\">first</a></li>");
        html.ShouldContain("<p>1 / 7</p>");
        html.ShouldNotContain("<hr />");

        var request = renderer.ContentListQuery.Requests.ShouldHaveSingleItem();
        request.MaxResultCount.ShouldBe(20);
        request.FieldFilters.ShouldBeSameAs(viewModel.FieldFilters);
    }

    [Fact]
    public async Task The_Title_Should_Fall_Back_To_The_Pages_Name_Without_Head_Metadata()
    {
        var viewModel = new SiteRenderViewModel
        {
            Page = ScribanTestRenderer.Page(),
            CultureName = "ja",
            FieldFilters = new Dictionary<string, string>()
        };

        var html = await new ScribanTestRenderer().RenderAsync(PageTemplate, viewModel: viewModel);

        html.ShouldContain("<title>About us</title>");
    }

    [Fact]
    public async Task The_Layout_Should_Wrap_A_Body()
    {
        var html = await new ScribanTestRenderer("en,ja", "/ja/about").RenderAsync("<p>Hello</p>", Layout);

        html.ShouldStartWith("<!DOCTYPE html>");
        html.ShouldContain("<title>About</title>");
        html.ShouldContain("<link href=\"https://acme.example/ja/about\" rel=\"canonical\">");
        html.ShouldContain("<nav><a href=\"/about\">en</a> <a href=\"/ja/about\">ja</a> </nav>");
        html.IndexOf("<p>Hello</p>", StringComparison.Ordinal).ShouldBeGreaterThan(html.IndexOf("</nav>", StringComparison.Ordinal));
        html.TrimEnd().ShouldEndWith("</html>");
    }
}
