using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.TagHelpers;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// <c>site_head { ... }</c>: what a template adds to the head tags Site derives from the route - a share image, the
/// og:type and structured data - and the Open Graph tags Site now writes from the route itself.
/// </summary>
public class ScribanSiteHeadOptions_Tests
{
    private static HeadMetadataDto Metadata(string? ogImageUrl = null, bool isDefault = false) => new()
    {
        MetaTitle = "About",
        BaseMetaTitle = "About",
        CanonicalUrl = "https://acme.example/ja/about",
        OgImageUrl = ogImageUrl,
        IsDefaultOgImage = isDefault,
        OgImageWidth = ogImageUrl != null && isDefault ? 1200 : null,
        OgImageHeight = ogImageUrl != null && isDefault ? 630 : null,
        OgType = "website",
        TwitterCardType = ogImageUrl == null ? "summary" : "summary_large_image",
        SiteName = "Acme",
        Locale = "ja_JP",
        LocaleAlternates = new List<string> { "en_US" },
        BaseUrl = "https://acme.example",
        HreflangAlternates = new List<HreflangAlternateDto>
        {
            new() { CultureName = "en", Url = "https://acme.example/about", Path = "/about" },
            new() { CultureName = "ja", Url = "https://acme.example/ja/about", Path = "/ja/about" }
        },
        Breadcrumb = new List<BreadcrumbItemDto>
        {
            new() { Name = "ホーム", Path = "/ja", Url = "https://acme.example/ja", PageName = "home" },
            new() { Name = "会社概要", Path = "/ja/about", Url = "https://acme.example/ja/about", PageName = "about", IsCurrent = true }
        }
    };

    private static SiteRenderViewModel ViewModel(HeadMetadataDto metadata, bool withContent = false)
    {
        var source = withContent ? ScribanTestRenderer.ContentViewModel() : ScribanTestRenderer.PageViewModel();
        return new SiteRenderViewModel
        {
            Page = source.Page,
            CultureName = source.CultureName,
            FieldFilters = source.FieldFilters,
            HeadMetadata = metadata,
            Content = source.Content
        };
    }

    [Fact]
    public async Task Site_Head_Should_Write_The_Site_Name_And_Locales()
    {
        var html = await new ScribanTestRenderer().RenderAsync("{{ site_head }}", viewModel: ViewModel(Metadata()));

        html.ShouldContain("<meta content=\"Acme\" property=\"og:site_name\">");
        html.ShouldContain("<meta content=\"ja_JP\" property=\"og:locale\">");
        html.ShouldContain("<meta content=\"en_US\" property=\"og:locale:alternate\">");
        html.ShouldContain("<meta content=\"summary\" name=\"twitter:card\">");
    }

    /// <summary>A template's image - a file field's value - takes the place of the default, sized and made absolute.</summary>
    [Fact]
    public async Task A_Template_Image_Should_Replace_The_Default_Share_Image()
    {
        var viewModel = ViewModel(Metadata("https://acme.example/api/site-public/files/images/default.jpg?Width=1200&Height=630", isDefault: true));

        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ cover = [{ url: \"/api/site-public/files/images/cover.jpg\" }] }}{{ site_head { image: cover } }}",
            viewModel: viewModel);

        html.ShouldContain("content=\"https://acme.example/api/site-public/files/images/cover.jpg?Width=1200&amp;Height=630\" property=\"og:image\"");
        html.ShouldNotContain("default.jpg");
        html.ShouldContain("<meta content=\"1200\" property=\"og:image:width\">");
        html.ShouldContain("<meta content=\"summary_large_image\" name=\"twitter:card\">");
    }

    [Fact]
    public async Task A_Template_Image_Should_Not_Replace_The_Authors()
    {
        var viewModel = ViewModel(Metadata("https://cdn.example/own.jpg"));

        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ site_head { image: \"https://cdn.example/cover.jpg\" } }}", viewModel: viewModel);

        html.ShouldContain("content=\"https://cdn.example/own.jpg\" property=\"og:image\"");
        html.ShouldNotContain("cover.jpg");
        html.ShouldNotContain("og:image:width");
    }

    [Fact]
    public async Task An_Article_Should_Carry_Its_Times()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ site_head { type: \"article\" } }}", viewModel: ViewModel(Metadata(), withContent: true));

        html.ShouldContain("<meta content=\"article\" property=\"og:type\">");
        html.ShouldNotContain("content=\"website\"");
        html.ShouldContain("property=\"article:published_time\"");
    }

    [Fact]
    public async Task The_Schema_Should_Be_Written_With_Site_Addresses_In_The_Pages_Language()
    {
        const string template = """
            {{ crumbs = [] }}
            {{ for c in breadcrumb }}
              {{ crumbs = array.add crumbs { "@type": "ListItem", position: for.index + 1, name: c.name, item: c.url } }}
            {{ end }}
            {{ site_head { schema: [
              { "@type": "Organization", "@id": "~/#org", name: "Acme", url: "~/", logo: "/api/site-public/files/images/logo.png" },
              { "@type": "BreadcrumbList", itemListElement: crumbs }
            ] } }}
            """;

        var html = await new ScribanTestRenderer().RenderAsync(template, viewModel: ViewModel(Metadata()));

        html.ShouldContain("<script type=\"application/ld+json\">{\"@context\":\"https://schema.org\",\"@graph\":[");
        // ~/ is localized as url "~/" is: the Japanese home page is /ja.
        html.ShouldContain("\"@id\":\"https://acme.example/ja#org\"");
        html.ShouldContain("\"url\":\"https://acme.example/ja\"");
        html.ShouldContain("\"logo\":\"https://acme.example/api/site-public/files/images/logo.png\"");
        html.ShouldContain("{\"@type\":\"ListItem\",\"position\":2,\"name\":\"会社概要\",\"item\":\"https://acme.example/ja/about\"}");
    }

    [Fact]
    public async Task Breadcrumb_Should_Be_Template_Data()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ site_head }}{{ for c in breadcrumb }}[{{ c.name }}|{{ c.path }}|{{ c.page_name }}|{{ c.is_current }}]{{ end }}",
            viewModel: ViewModel(Metadata()));

        html.ShouldContain("[ホーム|/ja|home|false][会社概要|/ja/about|about|true]");
    }

    /// <summary>
    /// The breadcrumb example of "The Head" in scriban-templates.md, verbatim: a step whose page has a text of its own in
    /// the theme is labelled with it, the others keep their name.
    /// </summary>
    [Fact]
    public async Task Breadcrumb_Steps_Can_Be_Labelled_With_The_Themes_Own_Texts()
    {
        const string template = """
            {{ site_head }}
            {{ for step in breadcrumb }}
              <a href="{{ step.path }}">{{ step.slug ? step.name : (t ("Breadcrumb:" + step.page_name) step.name) }}</a>
            {{ end }}
            """;
        var renderSet = ScribanTestRenderer.RenderSet(new Dictionary<string, string>
        {
            [ScribanTestRenderer.PagePath] = template,
            ["locales/ja.json"] = "{ \"culture\": \"ja\", \"texts\": { \"Breadcrumb:about\": \"概要\" } }"
        });

        var html = await new ScribanTestRenderer().RenderAsync(renderSet, ViewModel(Metadata()));

        html.ShouldContain("<a href=\"/ja\">ホーム</a>");
        html.ShouldContain("<a href=\"/ja/about\">概要</a>");
    }

    [Theory]
    [InlineData("{{ site_head { image: \"x\", colour: \"red\" } }}", "no option `colour`")]
    [InlineData("{{ site_head { type: \"product\" } }}", "type is `product`")]
    [InlineData("{{ site_head { schema: { name: \"Acme\" } } }}", "@type")]
    [InlineData("{{ site_head { title: \"x\" } \"description\" }}", "not both")]
    public async Task A_Wrong_Option_Should_Fail_The_Render(string template, string message)
    {
        var ex = await Should.ThrowAsync<Exception>(() =>
            new ScribanTestRenderer().RenderAsync(template, viewModel: ViewModel(Metadata())));

        (ex.Message + (ex as BusinessException)?.Details + ex.InnerException?.Message).ShouldContain(message);
    }

    /// <summary>The tag helper and site_head write the same tags for the same options.</summary>
    [Fact]
    public async Task Site_Head_Should_Write_What_The_Tag_Helper_Writes()
    {
        var metadata = Metadata("https://acme.example/default.jpg", isDefault: true);

        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ site_head { title: \"About - 2\", image: \"https://cdn.example/x.jpg\" } }}", viewModel: ViewModel(metadata));

        html.ShouldBe(SiteHeadMetadataHtml.Build(
            metadata,
            new SiteHeadOptions { Title = "About - 2", Image = "https://cdn.example/x.jpg" },
            1,
            "page",
            ScribanTestRenderer.Encoder));
    }
}
