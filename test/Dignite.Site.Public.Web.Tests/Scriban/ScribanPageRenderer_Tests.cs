using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.ContentTypes;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Contents;
using Dignite.Site.Public.Scriban.Functions;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Themes;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Public.Scriban;

public class ScribanPageRenderer_Tests
{
    /// <summary>A layout that only adds the head tags, for tests about the page template itself.</summary>
    private const string HeadOnlyLayout = "{{ site_head }}{{ body }}";

    [Fact]
    public async Task A_Page_Template_On_Its_Own_Should_Render()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "<head>{{ site_head }}</head><h1>{{ page.display_name }}</h1>");

        html.ShouldContain("<link href=\"https://acme.example/ja/about\" rel=\"canonical\">");
        html.ShouldContain("<h1>About us</h1>");
    }

    [Fact]
    public async Task Site_Head_Should_Write_What_The_Tag_Helper_Writes()
    {
        var html = await new ScribanTestRenderer().RenderAsync("{{ site_head \"About - page 2\" \"Template copy.\" }}");

        html.ShouldBe(SiteHeadMetadataHtml.Build(ScribanTestRenderer.HeadMetadata, "About - page 2", "Template copy.", 1, "page"));
        html.ShouldContain("<meta content=\"About - page 2\" property=\"og:title\">");
    }

    /// <summary>
    /// Beyond page 1 every address site_head writes carries the page number, as the tag helper's current-page
    /// makes it do - without the template passing anything: the request's page number is the model's.
    /// </summary>
    [Fact]
    public async Task Site_Head_Should_Carry_The_Requests_Page_Number()
    {
        var viewModel = ScribanTestRenderer.PageViewModel(currentPage: 2);

        var html = await new ScribanTestRenderer().RenderAsync("{{ site_head }}", viewModel: viewModel);

        html.ShouldBe(SiteHeadMetadataHtml.Build(ScribanTestRenderer.HeadMetadata, null, null, 2, "page"));
        html.ShouldContain("<link href=\"https://acme.example/ja/about?page=2\" rel=\"canonical\">");
        html.ShouldContain("href=\"https://acme.example/about?page=2\" hreflang=\"en\"");
    }

    /// <summary>The query key is the host's: SiteScribanOptions.PageQueryKey names it for site_head and page_url alike.</summary>
    [Fact]
    public async Task Site_Head_And_Page_Url_Should_Use_The_Configured_Page_Query_Key()
    {
        var renderer = new ScribanTestRenderer(options: new SiteScribanOptions { PageQueryKey = "p" });

        var html = await renderer.RenderAsync(
            "{{ site_head }}|{{ page_url 3 }}",
            viewModel: ScribanTestRenderer.PageViewModel(currentPage: 2));

        html.ShouldContain("<link href=\"https://acme.example/ja/about?p=2\" rel=\"canonical\">");
        html.ShouldEndWith("|/ja/about?p=3");
    }

    [Fact]
    public async Task The_Layout_Should_Wrap_The_Page_Templates_Output()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "<p>{{ page.name }}</p>",
            "<html><head>{{ site_head }}</head><main>{{ body }}</main></html>");

        html.ShouldStartWith("<html><head><link href=\"https://acme.example/ja/about\" rel=\"canonical\">");
        html.ShouldEndWith("<main><p>about</p></main></html>");
    }

    [Fact]
    public async Task A_Variable_The_Page_Template_Assigns_Should_Reach_The_Layout()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ title = \"Our story\" }}<p>body</p>",
            "<title>{{ title }}</title>{{ site_head }}{{ body }}");

        html.ShouldStartWith("<title>Our story</title>");
    }

    /// <summary>
    /// Variables are strict: a layout reading one its page did not assign fails. Read through <c>this</c>, a variable
    /// that is not there is null instead - the documented way for a layout to take an optional one from its pages.
    /// </summary>
    [Fact]
    public async Task A_Layout_Should_Read_A_Variable_Its_Page_May_Leave_Out_Through_This()
    {
        const string layout = "{{ site_head }}<title>{{ this.title ?? page.display_name }}</title>{{ body }}";
        var renderer = new ScribanTestRenderer();

        (await renderer.RenderAsync("{{ title = \"Mine\" }}<p>x</p>", layout)).ShouldEndWith("<title>Mine</title><p>x</p>");
        (await renderer.RenderAsync("<p>x</p>", layout)).ShouldEndWith("<title>About us</title><p>x</p>");

        var strict = await Record.ExceptionAsync(() => renderer.RenderAsync("<p>x</p>", "{{ site_head }}<title>{{ title ?? \"none\" }}</title>{{ body }}"));
        strict.ShouldNotBeNull();
        strict.Message.ShouldContain("title");
    }

    /// <summary>
    /// What the docs warn of: inside an object literal <c>this</c> is the object being built, so a layout passing an
    /// optional variable to <c>site_head</c> reads it into a variable first.
    /// </summary>
    [Fact]
    public async Task This_Inside_An_Object_Literal_Should_Be_The_Object_Being_Built()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ title = \"Mine\" }}",
            "{{ site_head }}[{{ o = { t: this.title } }}{{ o.t ?? \"empty\" }}][{{ og_title = this.title }}{{ p = { t: og_title } }}{{ p.t }}]{{ body }}");

        html.ShouldEndWith("[empty][Mine]");
    }

    [Fact]
    public async Task A_Render_That_Never_Calls_Site_Head_Should_Fail()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => new ScribanTestRenderer().RenderAsync(
            "{{ if false }}{{ site_head }}{{ end }}<p>no head</p>"));

        ex.Code.ShouldBe("Site:090005");
        ex.Data["Name"].ShouldBe("Default");
    }

    [Fact]
    public async Task A_Layout_That_Never_Calls_Site_Head_Should_Fail_Naming_The_Layout()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => new ScribanTestRenderer().RenderAsync(
            "<p>page</p>", "{{ if false }}{{ site_head }}{{ end }}{{ body }}"));

        ex.Data["Name"].ShouldBe(ScribanTestRenderer.LayoutPath);
    }

    [Fact]
    public async Task A_Page_Should_Render_In_The_Default_Layout()
    {
        var render = await new ScribanTestRenderer().RenderPageAsync(ScribanTestRenderer.RenderSet(
            "<p>page</p>", "{{ site_head }}<main>{{ body }}</main>"));

        render.LayoutPath.ShouldBe(ScribanTestRenderer.LayoutPath);
        render.Html.ShouldEndWith("<main><p>page</p></main>");
    }

    [Fact]
    public async Task A_Page_Should_Render_In_The_Layout_Its_Front_Matter_Names()
    {
        var render = await new ScribanTestRenderer().RenderPageAsync(ScribanTestRenderer.RenderSet(
            new Dictionary<string, string>
            {
                [ScribanTestRenderer.PagePath] = "+++\nlayout = \"layouts/landing\"\n+++\n<p>page</p>",
                [ScribanTestRenderer.LayoutPath] = "{{ site_head }}<main>{{ body }}</main>",
                ["layouts/landing"] = "{{ site_head }}<section class=\"landing\">{{ body }}</section>"
            },
            ScribanTestRenderer.LayoutPath));

        render.LayoutPath.ShouldBe("layouts/landing");
        render.Html.ShouldEndWith("<section class=\"landing\"><p>page</p></section>");
    }

    [Fact]
    public async Task A_Page_Should_Render_Alone_With_Layout_None()
    {
        var render = await new ScribanTestRenderer().RenderPageAsync(ScribanTestRenderer.RenderSet(
            "+++\nlayout = \"none\"\n+++\n{{ site_head }}<p>page</p>", "{{ site_head }}<main>{{ body }}</main>"));

        render.LayoutPath.ShouldBeNull();
        render.Html.ShouldEndWith("<p>page</p>");
        render.Html.ShouldNotContain("<main>");
    }

    [Fact]
    public async Task A_Page_Should_Render_Alone_Without_A_Default_Layout()
    {
        var render = await new ScribanTestRenderer().RenderPageAsync(ScribanTestRenderer.RenderSet(
            new Dictionary<string, string> { [ScribanTestRenderer.PagePath] = "{{ site_head }}<p>page</p>" }));

        render.LayoutPath.ShouldBeNull();
        render.Html.ShouldEndWith("<p>page</p>");
    }

    [Fact]
    public async Task A_Layout_That_Is_Not_In_The_Theme_Should_Fail_Naming_It()
    {
        var ex = await Should.ThrowAsync<ScribanRenderException>(() => new ScribanTestRenderer().RenderPageAsync(
            ScribanTestRenderer.RenderSet("+++\nlayout = \"layouts/gone\"\n+++\n<p>page</p>", "{{ site_head }}{{ body }}")));

        ex.Message.ShouldContain("'layouts/gone'");
    }

    /// <summary>A page template can be anywhere: the path is the page's Template, directories and all.</summary>
    [Fact]
    public async Task A_Page_Template_Should_Be_Found_By_Its_Full_Path()
    {
        var render = await new ScribanTestRenderer().RenderPageAsync(
            ScribanTestRenderer.RenderSet(
                new Dictionary<string, string>
                {
                    ["blog/post"] = "<p>post</p>",
                    [ScribanTestRenderer.LayoutPath] = "{{ site_head }}{{ body }}"
                },
                ScribanTestRenderer.LayoutPath),
            templatePath: "blog/post");

        render.Html.ShouldEndWith("<p>post</p>");
    }

    [Fact]
    public async Task T_Should_Read_The_Themes_Texts_With_Fallbacks()
    {
        var html = await new ScribanTestRenderer().RenderAsync(ScribanTestRenderer.RenderSet(
            "[{{ t \"Blog:Name\" }}][{{ \"Blog:Name\" | t }}][{{ t \"Blog:Missing\" \"Fallback\" }}][{{ t \"Blog:Missing\" }}]",
            HeadOnlyLayout,
            assets: ScribanTestRenderer.JapaneseTexts));

        html.ShouldEndWith("[ブログ][ブログ][Fallback][Blog:Missing]");
    }

    /// <summary>A page in Japanese on a site whose default language is English: the English text stands in for a Japanese one the theme lacks.</summary>
    [Fact]
    public async Task T_Should_Fall_Back_To_The_Sites_Default_Language()
    {
        var html = await new ScribanTestRenderer("en,ja", "/ja/about").RenderAsync(ScribanTestRenderer.RenderSet(
            "[{{ t \"Blog:Name\" }}][{{ t \"Menu:About\" }}][{{ t \"Nowhere\" }}]",
            HeadOnlyLayout,
            assets: new Dictionary<string, string>
            {
                ["locales/ja.json"] = "{ \"culture\": \"ja\", \"texts\": { \"Blog:Name\": \"ブログ\" } }",
                ["locales/en.json"] = "{ \"culture\": \"en\", \"texts\": { \"Blog:Name\": \"Blog\", \"Menu:About\": \"About\" } }"
            }));

        html.ShouldEndWith("[ブログ][About][Nowhere]");
    }

    /// <summary>The renderer loads the site's languages itself and hands the default one to its functions, rather than leaving them to find it loaded.</summary>
    [Fact]
    public async Task The_Renderer_Should_Hand_Its_Functions_The_Sites_Default_Language()
    {
        var renderer = new ScribanTestRenderer("ja,en", "/en/about");

        await renderer.RenderAsync("{{ site_head }}");

        renderer.LastScope!.DefaultCultureName.ShouldBe("ja");
    }

    /// <summary>A theme shows its own texts and nobody else's: two themes through one renderer, and one with no texts at all.</summary>
    [Fact]
    public async Task T_Should_Read_Only_The_Texts_Of_The_Theme_It_Renders()
    {
        const string page = "[{{ t \"Greeting\" \"none\" }}]";
        var renderer = new ScribanTestRenderer();

        string Theme(string greeting) => $"{{ \"culture\": \"ja\", \"texts\": {{ \"Greeting\": \"{greeting}\" }} }}";

        var acme = await renderer.RenderAsync(ScribanTestRenderer.RenderSet(
            page, HeadOnlyLayout, assets: new Dictionary<string, string> { ["locales/ja.json"] = Theme("Acme") }));
        var globex = await renderer.RenderAsync(ScribanTestRenderer.RenderSet(
            page, HeadOnlyLayout, assets: new Dictionary<string, string> { ["locales/ja.json"] = Theme("Globex") }));
        var plain = await renderer.RenderAsync(ScribanTestRenderer.RenderSet(page, HeadOnlyLayout));

        acme.ShouldEndWith("[Acme]");
        globex.ShouldEndWith("[Globex]");
        plain.ShouldEndWith("[none]");
    }

    [Fact]
    public async Task Url_Should_Keep_A_Link_In_The_Pages_Language()
    {
        var html = await new ScribanTestRenderer("en,ja", "/ja/about").RenderAsync(
            "[{{ url \"~/contact?tab=2\" }}][{{ url \"~/contact\" \"en\" }}][{{ url \"~/Account/Login\" }}][{{ url \"https://example.com/\" }}]",
            HeadOnlyLayout);

        html.ShouldEndWith("[/ja/contact?tab=2][/contact][/Account/Login][https://example.com/]");
    }

    /// <summary>
    /// page_no is the request's page number, and page_url the current path with it: no query for page 1, the
    /// configured key beyond, under the host's base path.
    /// </summary>
    [Fact]
    public async Task Page_No_And_Page_Url_Should_Follow_The_Requests_Page()
    {
        var renderer = new ScribanTestRenderer("en,ja", "/ja/about");
        renderer.Host.HttpContext.Request.PathBase = "/site";

        var html = await renderer.RenderAsync(
            "[{{ page_no }}][{{ page_url 1 }}][{{ page_url page_no + 1 }}][{{ page_url 0 }}]",
            HeadOnlyLayout,
            ScribanTestRenderer.PageViewModel(currentPage: 2));

        html.ShouldEndWith("[2][/site/ja/about][/site/ja/about?page=3][/site/ja/about]");
    }

    /// <summary>The pager the documentation shows, verbatim: keep it identical to the Paging section.</summary>
    [Fact]
    public async Task The_Documented_Pager_Should_Render()
    {
        var renderer = new ScribanTestRenderer("en,ja", "/ja/about");
        renderer.ContentListQuery.Items.Add(ScribanTestRenderer.Content("first", "First post"));
        renderer.ContentListQuery.TotalCount = 25;

        var html = await renderer.RenderAsync(
            """
            {{ list = contents { max: 10, page_no: page_no } }}
            <ul>
            {{ for item in list.items }}
                <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
            {{ end }}
            </ul>
            {{ if page_no > 1 }}<a href="{{ page_url page_no - 1 }}">Newer</a>{{ end }}
            {{ if page_no * list.max_result_count < list.total_count }}<a href="{{ page_url page_no + 1 }}">Older</a>{{ end }}
            """,
            HeadOnlyLayout,
            ScribanTestRenderer.PageViewModel(currentPage: 2));

        renderer.ContentListQuery.Requests.ShouldHaveSingleItem().CurrentPage.ShouldBe(2);
        html.ShouldContain("<li><a href=\"/ja/about/first\">first</a></li>");
        html.ShouldContain("<a href=\"/ja/about\">Newer</a>");
        html.ShouldContain("<a href=\"/ja/about?page=3\">Older</a>");
    }

    [Fact]
    public async Task Page_No_Should_Be_One_By_Default()
    {
        var html = await new ScribanTestRenderer().RenderAsync("[{{ page_no }}][{{ page_url page_no }}]", HeadOnlyLayout);

        html.ShouldEndWith("[1][/ja/about]");
    }

    [Fact]
    public async Task Url_Should_Leave_A_Default_Language_Link_Unprefixed()
    {
        var html = await new ScribanTestRenderer("en,ja", "/about").RenderAsync("[{{ url \"~/contact\" }}]", HeadOnlyLayout);

        html.ShouldEndWith("[/contact]");
    }

    [Fact]
    public async Task File_Url_Should_Size_A_Site_Image()
    {
        const string url = "/api/site-public/files/images/a.png?__tenant=";

        var html = await new ScribanTestRenderer().RenderAsync(
            $"[{{{{ file_url \"{url}\" 300 }}}}][{{{{ file_url \"{url}\" 300 200 }}}}]", HeadOnlyLayout);

        // Encoded like every value - in an attribute, "&amp;" is what a browser reads back as "&".
        html.ShouldEndWith(
            $"[{ScribanTestRenderer.Encoder.Encode(Dignite.Site.Files.SiteFileUrl.Sized(url, 300)!)}]" +
            $"[{ScribanTestRenderer.Encoder.Encode(Dignite.Site.Files.SiteFileUrl.Sized(url, 300, 200)!)}]");
        html.ShouldContain("Width=300&amp;Height=200");
    }

    /// <summary>Under the host's base path, never with the page's language prefix (this page is /ja/about).</summary>
    [Fact]
    public async Task Asset_Url_Should_Carry_The_Content_Hash()
    {
        var html = await new ScribanTestRenderer("en,ja", "/ja/about").RenderAsync(ScribanTestRenderer.RenderSet(
            "<link href=\"{{ asset_url \"assets/site.css\" }}\" rel=\"stylesheet\">[{{ asset_url \"/js/site.js\" }}]",
            HeadOnlyLayout,
            assets: new Dictionary<string, string>
            {
                ["assets/site.css"] = "body{}",
                ["js/site.js"] = "console.log(1);"
            }));

        html.ShouldEndWith(
            $"<link href=\"/site-assets/assets/site.css?v={SiteThemePaths.ComputeContentHash("body{}")}\" rel=\"stylesheet\">" +
            $"[/site-assets/js/site.js?v={SiteThemePaths.ComputeContentHash("console.log(1);")}]");
    }

    [Fact]
    public async Task Asset_Url_Of_A_Missing_File_Should_Fail_Naming_It()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(ScribanTestRenderer.RenderSet(
            "{{ asset_url \"assets/missing.css\" }}",
            HeadOnlyLayout,
            assets: new Dictionary<string, string> { ["assets/site.css"] = "" })));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("'assets/missing.css'");
    }

    [Fact]
    public async Task Asset_Url_Of_A_Template_Should_Fail_Naming_It()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(ScribanTestRenderer.RenderSet(
            "{{ asset_url \"_card\" }}",
            HeadOnlyLayout,
            partials: new Dictionary<string, string> { ["_card"] = "" })));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("'_card' is a template");
    }

    [Fact]
    public async Task Contents_Without_A_Page_Should_List_The_Current_Pages_Slice()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.Items.Add(ScribanTestRenderer.Content("first", "First post"));
        renderer.ContentListQuery.TotalCount = 7;
        var viewModel = ScribanTestRenderer.PageViewModel();

        var html = await renderer.RenderAsync(
            ScribanTestRenderer.RenderSet(
                "{{ list = contents { max: 5, page_no: 2 } }}{{ list.total_count }}|{{ list.current_page }}|" +
                "{{ for item in list.items }}{{ item.slug }}:{{ item.fields.Title }}:{{ item.publish_time.year }}{{ end }}",
                HeadOnlyLayout),
            viewModel);

        html.ShouldEndWith("7|2|first:First post:2026");

        var request = renderer.ContentListQuery.Requests.ShouldHaveSingleItem();
        request.Page.ShouldBeSameAs(viewModel.Page);
        request.CultureName.ShouldBe("ja");
        request.MaxResultCount.ShouldBe(5);
        request.CurrentPage.ShouldBe(2);
        request.FieldFilters.ShouldBeSameAs(viewModel.FieldFilters);
        request.PublishedAfter.ShouldBe(viewModel.PublishedAfter);
        request.PublishedBefore.ShouldBe(viewModel.PublishedBefore);
        renderer.ContentListQuery.PagesLookedUp.ShouldBeEmpty();
    }

    /// <summary>
    /// A page number whose skip count would overflow an int is capped: the query then asks for a page past the end,
    /// which lists nothing, rather than for a negative skip count the content service refuses.
    /// </summary>
    [Fact]
    public async Task Contents_With_A_Page_Number_Beyond_The_Cap_Should_Ask_For_The_Last_Page_That_Fits()
    {
        var renderer = new ScribanTestRenderer();

        var html = await renderer.RenderAsync(
            ScribanTestRenderer.RenderSet(
                "{{ list = contents { max: 100, page_no: 2147483647 } }}{{ list.current_page }}|{{ list.items.size }}",
                HeadOnlyLayout));

        html.ShouldEndWith(ContentsFunction.MaxPageNumber + "|0");

        var request = renderer.ContentListQuery.Requests.ShouldHaveSingleItem();
        request.CurrentPage.ShouldBe(ContentsFunction.MaxPageNumber);
        ((long)(request.CurrentPage - 1) * request.MaxResultCount).ShouldBeLessThanOrEqualTo(int.MaxValue);
    }

    [Fact]
    public async Task Contents_Of_Another_Page_Should_Not_Carry_The_Current_Routes_Filters()
    {
        var renderer = new ScribanTestRenderer();
        var news = ScribanTestRenderer.Page();
        renderer.ContentListQuery.PagesByName["news"] = news;

        await renderer.RenderAsync(
            "{{ list = contents { page: \"news\", content_type: \"post\", filter: \"trip\", max: 1000 } }}{{ list.total_count }}",
            HeadOnlyLayout);

        var request = renderer.ContentListQuery.Requests.ShouldHaveSingleItem();
        request.Page.ShouldBeSameAs(news);
        request.ContentTypeName.ShouldBe("post");
        request.Filter.ShouldBe("trip");
        request.MaxResultCount.ShouldBe(100);
        request.FieldFilters.ShouldBeNull();
        request.PublishedAfter.ShouldBeNull();
    }

    [Fact]
    public async Task Contents_Of_A_Page_That_Does_Not_Exist_Should_List_Nothing()
    {
        var renderer = new ScribanTestRenderer();

        var html = await renderer.RenderAsync("{{ gone = contents { page: \"gone\" } }}{{ gone.total_count }}", HeadOnlyLayout);

        html.ShouldEndWith("0");
        renderer.ContentListQuery.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Contents_With_A_Misspelt_Option_Should_Fail()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ contents { pagesize: 5 } }}", HeadOnlyLayout));

        ex.ShouldNotBeNull();
        ex.Message.ShouldContain("pagesize");
    }

    [Fact]
    public async Task Content_Counts_Without_A_Page_Should_Count_The_Current_Pages_Slice()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.Counts.Add(new ContentFieldValueCountDto { Value = "travel", Count = 3 });
        renderer.ContentListQuery.Counts.Add(new ContentFieldValueCountDto { Value = "food", Count = 1 });
        var viewModel = ScribanTestRenderer.PageViewModel();

        var html = await renderer.RenderAsync(
            ScribanTestRenderer.RenderSet(
                "{{ for group in content_counts { field: \"category\" } }}{{ group.value }}={{ group.count }};{{ end }}",
                HeadOnlyLayout),
            viewModel);

        html.ShouldEndWith("travel=3;food=1;");

        var (request, fieldName, maxResultCount) = renderer.ContentListQuery.CountRequests.ShouldHaveSingleItem();
        fieldName.ShouldBe("category");
        maxResultCount.ShouldBe(100);
        request.Page.ShouldBeSameAs(viewModel.Page);
        request.CultureName.ShouldBe("ja");
        request.FieldFilters.ShouldBeSameAs(viewModel.FieldFilters);
        request.PublishedAfter.ShouldBe(viewModel.PublishedAfter);
        request.PublishedBefore.ShouldBe(viewModel.PublishedBefore);
        renderer.ContentListQuery.PagesLookedUp.ShouldBeEmpty();
    }

    [Fact]
    public async Task Content_Counts_Of_Another_Page_Should_Not_Carry_The_Current_Routes_Filters()
    {
        var renderer = new ScribanTestRenderer();
        var news = ScribanTestRenderer.Page();
        renderer.ContentListQuery.PagesByName["news"] = news;

        await renderer.RenderAsync(
            "{{ content_counts { field: \"category\", page: \"news\", content_type: \"post\", filter: \"trip\", max: 5000 } | array.size }}",
            HeadOnlyLayout);

        var (request, fieldName, maxResultCount) = renderer.ContentListQuery.CountRequests.ShouldHaveSingleItem();
        fieldName.ShouldBe("category");
        maxResultCount.ShouldBe(ContentCountsFunction.MaxGroups);
        request.Page.ShouldBeSameAs(news);
        request.ContentTypeName.ShouldBe("post");
        request.Filter.ShouldBe("trip");
        request.FieldFilters.ShouldBeNull();
        request.PublishedAfter.ShouldBeNull();
    }

    [Fact]
    public async Task Content_Counts_Of_A_Page_That_Does_Not_Exist_Should_Count_Nothing()
    {
        var renderer = new ScribanTestRenderer();

        var html = await renderer.RenderAsync(
            "{{ content_counts { field: \"category\", page: \"gone\" } | array.size }}", HeadOnlyLayout);

        html.ShouldEndWith("0");
        renderer.ContentListQuery.CountRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Content_Counts_Without_A_Field_Or_With_A_Misspelt_Option_Should_Fail()
    {
        var noField = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ content_counts { max: 5 } }}", HeadOnlyLayout));
        noField.ShouldNotBeNull();
        noField.Message.ShouldContain("name of the field");

        var misspelt = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ content_counts { field: \"category\", limit: 5 } }}", HeadOnlyLayout));
        misspelt.ShouldNotBeNull();
        misspelt.Message.ShouldContain("limit");
    }

    /// <summary>
    /// Another page's slice, filtered as its route would filter it: the options' field values - text, a ticked box,
    /// an empty value left out - and publish dates, as a date or as text.
    /// </summary>
    [Fact]
    public async Task Contents_Of_Another_Page_Should_Take_Its_Filters_From_The_Options()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.PagesByName["news"] = ScribanTestRenderer.Page();

        await renderer.RenderAsync(
            "{{ list = contents { page: \"news\", field_filters: { category: \"travel\", featured: true, empty: \"\" }, " +
            "published_after: date.parse \"2026-01-01\", published_before: \"2026-12-31T23:59:59\" } }}{{ list.total_count }}",
            HeadOnlyLayout);

        var request = renderer.ContentListQuery.Requests.ShouldHaveSingleItem();
        request.FieldFilters.ShouldNotBeNull();
        request.FieldFilters!.OrderBy(f => f.Key).ShouldBe(new[]
        {
            new KeyValuePair<string, string>("category", "travel"),
            new KeyValuePair<string, string>("featured", "true")
        });
        request.PublishedAfter.ShouldBe(new DateTime(2026, 1, 1));
        request.PublishedBefore.ShouldBe(new DateTime(2026, 12, 31, 23, 59, 59));
    }

    /// <summary>On the current page the options add to the route's filters: a field they name and a bound they set replace the route's.</summary>
    [Fact]
    public async Task Contents_Of_The_Current_Page_Should_Add_The_Options_Filters_To_The_Routes()
    {
        var renderer = new ScribanTestRenderer();
        var viewModel = ScribanTestRenderer.PageViewModel();

        await renderer.RenderAsync(
            ScribanTestRenderer.RenderSet(
                "{{ list = contents { field_filters: { category: \"design\", featured: true }, published_after: \"2026-06-01\" } }}",
                HeadOnlyLayout),
            viewModel);

        var request = renderer.ContentListQuery.Requests.ShouldHaveSingleItem();
        request.FieldFilters!.OrderBy(f => f.Key).ShouldBe(new[]
        {
            new KeyValuePair<string, string>("category", "design"),
            new KeyValuePair<string, string>("featured", "true")
        });
        request.PublishedAfter.ShouldBe(new DateTime(2026, 6, 1));
        request.PublishedBefore.ShouldBe(viewModel.PublishedBefore);
        viewModel.FieldFilters["category"].ShouldBe("engineering");
    }

    [Fact]
    public async Task Contents_With_Filters_Of_The_Wrong_Kind_Should_Fail_Naming_The_Option()
    {
        var notAnObject = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ contents { field_filters: \"category\" } }}", HeadOnlyLayout));
        notAnObject.ShouldBeAssignableTo<ScriptRuntimeException>();
        notAnObject!.Message.ShouldContain("field_filters");

        var notADate = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ contents { published_after: \"last year\" } }}", HeadOnlyLayout));
        notADate.ShouldBeAssignableTo<ScriptRuntimeException>();
        notADate!.Message.ShouldContain("published_after");
    }

    [Fact]
    public async Task Content_Counts_Of_Another_Page_Should_Take_Its_Filters_From_The_Options()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.PagesByName["news"] = ScribanTestRenderer.Page();

        await renderer.RenderAsync(
            "{{ content_counts { field: \"category\", page: \"news\", field_filters: { featured: true }, published_before: \"2026-07-01\" } | array.size }}",
            HeadOnlyLayout);

        var (request, _, _) = renderer.ContentListQuery.CountRequests.ShouldHaveSingleItem();
        request.FieldFilters!.ShouldBe(new[] { new KeyValuePair<string, string>("featured", "true") });
        request.PublishedAfter.ShouldBeNull();
        request.PublishedBefore.ShouldBe(new DateTime(2026, 7, 1));
    }

    [Fact]
    public async Task Adjacent_Contents_Should_Give_The_Older_And_The_Newer_Content()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.Previous = ScribanTestRenderer.Content("older", "Older post");
        renderer.ContentListQuery.Next = ScribanTestRenderer.Content("newer", "Newer post");
        var viewModel = ScribanTestRenderer.ContentViewModel();

        var html = await renderer.RenderAsync(
            ScribanTestRenderer.RenderSet(
                "{{ around = adjacent_contents { content_type: \"post\" } }}{{ around.previous.slug }}|{{ around.next.fields.Title }}|{{ around.next.url }}",
                HeadOnlyLayout),
            viewModel);

        html.ShouldEndWith("older|Newer post|/ja/about/newer");
        var (page, contentId, contentTypeName) = renderer.ContentListQuery.AdjacentRequests.ShouldHaveSingleItem();
        page.ShouldBeSameAs(viewModel.Page);
        contentId.ShouldBe(viewModel.Content!.Content.Id);
        contentTypeName.ShouldBe("post");
    }

    /// <summary>Null at either end; on a list page, with no content to be either side of, both null without asking.</summary>
    [Fact]
    public async Task Adjacent_Contents_Should_Be_Null_At_Either_End_And_On_A_List_Page()
    {
        const string template = "{{ around = adjacent_contents }}{{ around.previous == null }}|{{ around.next == null }}";

        var atTheEnd = new ScribanTestRenderer();
        atTheEnd.ContentListQuery.Previous = ScribanTestRenderer.Content("older", "Older post");
        (await atTheEnd.RenderAsync(ScribanTestRenderer.RenderSet(template, HeadOnlyLayout), ScribanTestRenderer.ContentViewModel()))
            .ShouldEndWith("false|true");

        var onAList = new ScribanTestRenderer();
        (await onAList.RenderAsync(ScribanTestRenderer.RenderSet(template, HeadOnlyLayout), ScribanTestRenderer.PageViewModel()))
            .ShouldEndWith("true|true");
        onAList.ContentListQuery.AdjacentRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Adjacent_Contents_With_A_Misspelt_Option_Should_Fail()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            ScribanTestRenderer.RenderSet("{{ adjacent_contents { type: \"post\" } }}", HeadOnlyLayout),
            ScribanTestRenderer.ContentViewModel()));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("`type`");
    }

    /// <summary>Arguments fill a text's placeholders as composite formatting does, numbers and dates in the page's language.</summary>
    [Fact]
    public async Task T_Should_Fill_The_Texts_Placeholders_In_The_Pages_Language()
    {
        var html = await new ScribanTestRenderer().RenderAsync(ScribanTestRenderer.RenderSet(
            "[{{ t \"Pager:Page\" null 2 5 }}][{{ t \"Price\" null 1234567 }}][{{ t \"Archive\" null (date.parse \"2026-08-15\") }}]" +
            "[{{ t \"Missing\" \"{0} items\" 3 }}][{{ t \"Pager:Page\" }}]",
            HeadOnlyLayout,
            assets: new Dictionary<string, string>
            {
                ["locales/ja.json"] =
                    "{ \"culture\": \"ja\", \"texts\": { \"Pager:Page\": \"{0} / {1} ページ\", \"Price\": \"{0:N0} 円\", \"Archive\": \"{0:yyyy年M月}の記事\" } }"
            }));

        html.ShouldEndWith("[2 / 5 ページ][1,234,567 円][2026年8月の記事][3 items][{0} / {1} ページ]");
    }

    [Fact]
    public async Task T_With_Too_Few_Arguments_Should_Fail_Naming_The_Text()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(ScribanTestRenderer.RenderSet(
            "{{ t \"Pager:Page\" null 2 }}",
            HeadOnlyLayout,
            assets: new Dictionary<string, string>
            {
                ["locales/ja.json"] = "{ \"culture\": \"ja\", \"texts\": { \"Pager:Page\": \"{0} / {1}\" } }"
            })));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("'Pager:Page'");
    }

    [Fact]
    public async Task Text_Width_Should_Count_A_Wide_Character_Twice()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "[{{ text_width \"日本語ab\" }}][{{ text_width \"About – Acme\" }}][{{ text_width \"\" }}][{{ text_width null }}]", HeadOnlyLayout);

        html.ShouldEndWith("[8][12][0][0]");
    }

    [Fact]
    public async Task App_File_Url_Should_Address_A_File_Of_The_Application_With_Its_Version()
    {
        var renderer = new ScribanTestRenderer();
        renderer.Host.HttpContext.Request.PathBase = "/site";
        const string template = "[{{ app_file_url \"/libs/abp/core/abp.js\" }}][{{ app_file_url \"~/images/logo.svg?x=1\" }}]";

        (await renderer.RenderAsync(template, HeadOnlyLayout)).ShouldEndWith("[/site/libs/abp/core/abp.js][/site/images/logo.svg?x=1]");

        renderer.Host.HttpContext.RequestServices = new WithFileVersions(renderer.Host.HttpContext.RequestServices);
        (await renderer.RenderAsync(template, HeadOnlyLayout))
            .ShouldEndWith("[/site/libs/abp/core/abp.js?v=hash][/site/images/logo.svg?x=1&amp;v=hash]");
    }

    [Theory]
    [InlineData("libs/abp/core/abp.js")]
    [InlineData("//cdn.example/abp.js")]
    [InlineData("https://cdn.example/abp.js")]
    public async Task App_File_Url_Of_A_Path_That_Is_Not_The_Applications_Should_Fail(string path)
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ app_file_url \"" + path + "\" }}", HeadOnlyLayout));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("starting with /");
    }

    /// <summary>The request's services with a file version provider that versions every file alike, as asp-append-version would.</summary>
    private sealed class WithFileVersions : IServiceProvider, IFileVersionProvider
    {
        private readonly IServiceProvider _inner;

        public WithFileVersions(IServiceProvider inner)
        {
            _inner = inner;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(IFileVersionProvider) ? this : _inner.GetService(serviceType);
        }

        public string AddFileVersionToPath(PathString requestPathBase, string path)
        {
            return path + (path.Contains('?') ? "&" : "?") + "v=hash";
        }
    }

    [Fact]
    public async Task Render_Field_Should_Render_The_Fields_Display_Partial()
    {
        var renderer = new ScribanTestRenderer();

        var html = await renderer.RenderAsync(
            "[{{ render_field \"Gallery\" }}][{{ render_field \"Missing\" }}]", HeadOnlyLayout, ScribanTestRenderer.ContentViewModel());

        html.ShouldEndWith("[<div class=\"partial\">FlexFields/FileExplorer</div>][]");

        var (partialName, model) = renderer.PartialRenderer.Calls.ShouldHaveSingleItem();
        partialName.ShouldBe("FlexFields/FileExplorer");
        var viewModel = model.ShouldBeOfType<FlexFieldViewModel>();
        viewModel.Field.Name.ShouldBe("Gallery");
        viewModel.ShowInList.ShouldBeFalse();
    }

    [Fact]
    public async Task Render_Field_On_A_List_Page_Should_Render_Nothing()
    {
        var html = await new ScribanTestRenderer().RenderAsync("[{{ render_field \"Gallery\" true }}]", HeadOnlyLayout);

        html.ShouldEndWith("[]");
    }

    [Fact]
    public async Task Find_Page_Should_Return_A_Page_With_Its_Content_Types()
    {
        var renderer = new ScribanTestRenderer();
        var fieldId = Guid.NewGuid();
        renderer.ContentListQuery.PagesByName["blog"] = BlogPage(fieldId);

        var html = await renderer.RenderAsync(
            "{{ blog = find_page \"blog\" }}{{ blog.display_name }};{{ for ct in blog.content_types }}{{ ct.name }}:{{ for f in ct.fields }}{{ f.field_id }},{{ f.required }}{{ end }}{{ end }}",
            HeadOnlyLayout);

        html.ShouldEndWith($"Blog;post:{fieldId},true");
    }

    [Fact]
    public async Task Find_Page_Should_Be_Empty_For_A_Name_No_Page_Has()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "[{{ find_page \"nowhere\" }}]{{ if find_page \"nowhere\" }}found{{ end }}", HeadOnlyLayout);

        html.ShouldEndWith("[]");
    }

    [Fact]
    public async Task Find_Page_Should_Look_A_Page_Up_Once_Per_Render()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.PagesByName["blog"] = BlogPage(Guid.NewGuid());

        await renderer.RenderAsync("{{ a = find_page \"blog\" }}{{ b = find_page \"blog\" }}{{ a.name }}{{ b.name }}{{ find_page \"nowhere\" }}{{ find_page \"nowhere\" }}", HeadOnlyLayout);

        renderer.ContentListQuery.PagesLookedUp.ShouldBe(new[] { "blog", "nowhere" });
    }

    [Fact]
    public async Task Field_Definitions_Should_Be_Keyed_By_Name_For_The_Current_Content_Type()
    {
        var renderer = new ScribanTestRenderer();
        var viewModel = ScribanTestRenderer.ContentViewModel();
        var usages = viewModel.Content!.ContentType.Fields;
        renderer.FieldService.Fields[usages[0].FieldId] = Field(usages[0].FieldId, "Title", "Text", "The title");
        renderer.FieldService.Fields[usages[1].FieldId] = Field(usages[1].FieldId, "Tags", "Select");

        var html = await renderer.RenderAsync(
            "{{ defs = field_definitions content_type.fields }}{{ defs.Title.field_type_name }};{{ defs.Title.description }};{{ defs.Tags.display_name }};{{ defs.Gallery }}",
            HeadOnlyLayout,
            viewModel);

        html.ShouldEndWith("Text;The title;Tags;");

        var call = renderer.FieldService.Calls.ShouldHaveSingleItem();
        call.ShouldBe(usages.ConvertAll(usage => usage.FieldId));
    }

    [Fact]
    public async Task Field_Definitions_Should_Resolve_The_Fields_Of_A_Found_Page_From_A_List_Page()
    {
        var renderer = new ScribanTestRenderer();
        var fieldId = Guid.NewGuid();
        renderer.ContentListQuery.PagesByName["blog"] = BlogPage(fieldId);
        renderer.FieldService.Fields[fieldId] = Field(fieldId, "category", "Select");

        var html = await renderer.RenderAsync(
            "{{ blog = find_page \"blog\" }}{{ defs = field_definitions blog.content_types[0].fields }}{{ defs.category.field_type_name }};{{ defs.category.id }}",
            HeadOnlyLayout);

        html.ShouldEndWith($"Select;{fieldId}");
    }

    [Fact]
    public async Task Field_Definitions_Should_Take_Ids_As_Well_As_Fields()
    {
        var renderer = new ScribanTestRenderer();
        var viewModel = ScribanTestRenderer.ContentViewModel();
        var fieldId = viewModel.Content!.ContentType.Fields[0].FieldId;
        renderer.FieldService.Fields[fieldId] = Field(fieldId, "Title", "Text");

        var html = await renderer.RenderAsync(
            "{{ a = field_definitions [content_type.fields[0].field_id] }}{{ b = field_definitions content_type.fields[0].field_id }}{{ a.Title.name }}{{ b.Title.name }}",
            HeadOnlyLayout,
            viewModel);

        html.ShouldEndWith("TitleTitle");
    }

    [Fact]
    public async Task Field_Definitions_Should_Fetch_A_Definition_Once_Per_Render()
    {
        var renderer = new ScribanTestRenderer();
        var viewModel = ScribanTestRenderer.ContentViewModel();
        var fieldId = viewModel.Content!.ContentType.Fields[0].FieldId;
        renderer.FieldService.Fields[fieldId] = Field(fieldId, "Title", "Text");

        await renderer.RenderAsync(
            "{{ a = field_definitions content_type.fields }}{{ b = field_definitions content_type.fields }}{{ c = field_definitions content_type.fields[0] }}",
            HeadOnlyLayout,
            viewModel);

        renderer.FieldService.Calls.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Field_Definitions_Should_Leave_Out_A_Field_No_Content_Type_Names()
    {
        var renderer = new ScribanTestRenderer();
        var stray = Field(Guid.NewGuid(), "stray", "Text");
        renderer.FieldService.Fields[stray.Id] = stray;

        var html = await renderer.RenderAsync(
            $"{{{{ defs = field_definitions \"{stray.Id}\" }}}}{{{{ defs | object.keys | array.size }}}};{{{{ field_definitions \"not an id\" | object.keys | array.size }}}}",
            HeadOnlyLayout,
            ScribanTestRenderer.ContentViewModel());

        html.ShouldEndWith("0;0");
        renderer.FieldService.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Field_Definitions_Should_Take_At_Most_A_Hundred_Fields()
    {
        var viewModel = ScribanTestRenderer.ContentViewModel();
        var usages = viewModel.Content!.ContentType.Fields;
        while (usages.Count <= 100)
        {
            usages.Add(new() { FieldId = Guid.NewGuid() });
        }

        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ field_definitions content_type.fields }}", HeadOnlyLayout, viewModel));

        ex.ShouldBeOfType<ScriptRuntimeException>().Message.ShouldContain("at most 100");
    }

    private static PageDto BlogPage(params Guid[] fieldIds) => new()
    {
        Id = Guid.NewGuid(),
        Name = "blog",
        DisplayName = "Blog",
        Route = "/blog",
        Template = "Default",
        IsActive = true,
        ContentTypes = new List<ContentTypeDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "post",
                DisplayName = "Post",
                Fields = fieldIds.Select(id => new ContentTypeFieldDto { FieldId = id, Required = true }).ToList()
            }
        }
    };

    private static FieldDto Field(Guid id, string name, string fieldTypeName, string? description = null) => new()
    {
        Id = id,
        Name = name,
        DisplayName = name,
        FieldTypeName = fieldTypeName,
        Description = description
    };

    [Fact]
    public async Task A_Function_Should_Not_Be_Assignable()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync("{{ site_head = 1 }}", HeadOnlyLayout));

        ex.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Render_Over_Time_Should_Fail_Naming_The_Template_And_The_Limit()
    {
        // LoopLimit off: nested loops share one iteration budget, which would stop this long before the clock.
        var renderer = new ScribanTestRenderer(options: new SiteScribanOptions { RenderTimeout = TimeSpan.FromMilliseconds(200), LoopLimit = 0 });

        var ex = await Should.ThrowAsync<TimeoutException>(() => renderer.RenderAsync(
            "{{ site_head }}{{ for i in 1..1000 }}{{ for j in 1..1000 }}{{ for k in 1..1000 }}{{ end }}{{ end }}{{ end }}"));

        ex.Message.ShouldContain("'Default'");
        ex.Message.ShouldContain("200 ms");
    }
}
