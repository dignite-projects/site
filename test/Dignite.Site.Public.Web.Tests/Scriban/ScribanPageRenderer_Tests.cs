using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.Public.TagHelpers;
using global::Scriban.Syntax;
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

        ex.Data["Name"].ShouldBe("layout");
    }

    [Fact]
    public async Task T_Should_Read_The_Sites_Texts_With_Fallbacks()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "[{{ t \"Blog:Name\" }}][{{ \"Blog:Name\" | t }}][{{ t \"Blog:Missing\" \"Fallback\" }}][{{ t \"Blog:Missing\" }}]",
            HeadOnlyLayout);

        html.ShouldEndWith("[ブログ][ブログ][Fallback][Blog:Missing]");
    }

    [Fact]
    public async Task Url_Should_Keep_A_Link_In_The_Pages_Language()
    {
        var html = await new ScribanTestRenderer("en,ja", "/ja/about").RenderAsync(
            "[{{ url \"~/contact?tab=2\" }}][{{ url \"~/contact\" \"en\" }}][{{ url \"~/Account/Login\" }}][{{ url \"https://example.com/\" }}]",
            HeadOnlyLayout);

        html.ShouldEndWith("[/ja/contact?tab=2][/contact][/Account/Login][https://example.com/]");
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
    public async Task Asset_Url_Should_Carry_The_Published_Version_Id()
    {
        var html = await new ScribanTestRenderer("en,ja", "/ja/about").RenderAsync(ScribanTestRenderer.RenderSet(
            "<link href=\"{{ asset_url \"assets/site.css\" }}\" rel=\"stylesheet\">[{{ asset_url \"/assets/js/site.js\" }}]",
            HeadOnlyLayout,
            assets: new Dictionary<string, Guid>
            {
                ["assets/site.css"] = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
                ["assets/js/site.js"] = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000c")
            }));

        html.ShouldEndWith(
            "<link href=\"/site-assets/site.css?v=aaaaaaaa000000000000000000000003\" rel=\"stylesheet\">" +
            "[/site-assets/js/site.js?v=bbbbbbbb00000000000000000000000c]");
    }

    [Fact]
    public async Task Asset_Url_Of_An_Unpublished_Asset_Should_Fail_Naming_It()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(ScribanTestRenderer.RenderSet(
            "{{ asset_url \"assets/missing.css\" }}",
            HeadOnlyLayout,
            assets: new Dictionary<string, Guid> { ["assets/site.css"] = Guid.NewGuid() })));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("'assets/missing.css'");
    }

    [Theory]
    [InlineData("site.css")]
    [InlineData("partials/card")]
    public async Task Asset_Url_Of_A_Name_That_Is_Not_An_Asset_Should_Fail_Naming_The_Prefix(string name)
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(ScribanTestRenderer.RenderSet(
            $"{{{{ asset_url \"{name}\" }}}}",
            HeadOnlyLayout,
            assets: new Dictionary<string, Guid> { ["assets/site.css"] = Guid.NewGuid() })));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("'assets/'");
        ex.Message.ShouldContain($"'{name}'");
    }

    [Fact]
    public async Task Contents_Without_A_Page_Should_List_The_Current_Pages_Slice()
    {
        var renderer = new ScribanTestRenderer();
        renderer.ContentListQuery.Items.Add(ScribanTestRenderer.Content("first", "First post"));
        renderer.ContentListQuery.TotalCount = 7;
        var viewModel = ScribanTestRenderer.PageViewModel();

        var html = await renderer.Renderer.RenderAsync(
            "Default",
            ScribanTestRenderer.RenderSet(
                "{{ list = contents { max: 5, page_no: 2 } }}{{ list.total_count }}|{{ list.current_page }}|" +
                "{{ for item in list.items }}{{ item.slug }}:{{ item.fields.Title }}:{{ item.publish_time.year }}{{ end }}",
                HeadOnlyLayout),
            viewModel,
            renderer.ActionContext);

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
    public async Task Select_Options_Should_List_Text_And_Value()
    {
        var renderer = new ScribanTestRenderer();

        var html = await renderer.RenderAsync(
            "{{ for o in select_options \"blog\" \"category\" }}{{ o.text }}={{ o.value }};{{ end }}", HeadOnlyLayout);

        html.ShouldEndWith("Engineering=engineering;Essays=essays;");
        renderer.SelectOptions.Calls.ShouldHaveSingleItem().ShouldBe(("blog", "category"));
    }

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
