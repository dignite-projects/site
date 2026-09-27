using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.TagHelpers;

public class SiteHeadMetadataTagHelper_Tests
{
    private static HeadMetadataDto Metadata() => new()
    {
        MetaTitle = "Blog",
        MetaDescription = "What we write about.",
        CanonicalUrl = "https://acme.example/blog",
        OgType = "website",
        TwitterCardType = "summary",
        HreflangAlternates = new List<HreflangAlternateDto>
        {
            new() { CultureName = "en", Url = "https://acme.example/blog" },
            new() { CultureName = "ja", Url = "https://acme.example/ja/blog" }
        },
        XDefaultUrl = "https://acme.example/blog"
    };

    [Fact]
    public void Should_Write_Every_Tag_The_Metadata_Carries()
    {
        var html = Render(new SiteHeadMetadataTagHelper { Metadata = Metadata() });

        html.ShouldContain("<meta content=\"What we write about.\" name=\"description\">");
        html.ShouldContain("<link href=\"https://acme.example/blog\" rel=\"canonical\">");
        html.ShouldContain("<meta content=\"Blog\" property=\"og:title\">");
        html.ShouldContain("<meta content=\"What we write about.\" property=\"og:description\">");
        html.ShouldContain("<meta content=\"https://acme.example/blog\" property=\"og:url\">");
        html.ShouldContain("<meta content=\"website\" property=\"og:type\">");
        html.ShouldContain("<meta content=\"summary\" name=\"twitter:card\">");
        html.ShouldContain("<link href=\"https://acme.example/ja/blog\" hreflang=\"ja\" rel=\"alternate\">");
        html.ShouldContain("<link href=\"https://acme.example/blog\" hreflang=\"x-default\" rel=\"alternate\">");
        html.ShouldNotContain("robots");
        html.ShouldNotContain("og:image");
    }

    [Fact]
    public void The_Contents_Own_Description_Should_Win_Over_The_Templates()
    {
        var html = Render(new SiteHeadMetadataTagHelper { Metadata = Metadata(), FallbackDescription = "Template copy." });

        html.ShouldNotContain("Template copy.");
    }

    [Fact]
    public void The_Templates_Description_Should_Fill_In_When_The_Content_Has_None()
    {
        var metadata = Metadata();
        metadata.MetaDescription = null;

        var html = Render(new SiteHeadMetadataTagHelper { Metadata = metadata, FallbackDescription = "Template copy." });

        html.ShouldContain("<meta content=\"Template copy.\" name=\"description\">");
        html.ShouldContain("<meta content=\"Template copy.\" property=\"og:description\">");
    }

    [Fact]
    public void The_Templates_Title_Should_Win_For_Og_Title()
    {
        var html = Render(new SiteHeadMetadataTagHelper { Metadata = Metadata(), Title = "Blog - Page 2" });

        html.ShouldContain("<meta content=\"Blog - Page 2\" property=\"og:title\">");
    }

    [Fact]
    public void No_Metadata_Should_Write_Only_The_Templates_Description()
    {
        var html = Render(new SiteHeadMetadataTagHelper { FallbackDescription = "Template copy." });

        html.Trim().ShouldBe("<meta content=\"Template copy.\" name=\"description\">");
    }

    /// <summary>
    /// Page 2 lists different items than page 1: every address it writes stays on page 2, the other
    /// languages' alternates included, or the cluster does not reference the page being rendered.
    /// </summary>
    [Fact]
    public void Every_Address_Should_Keep_The_Page_Number_Beyond_Page_One()
    {
        var html = Render(new SiteHeadMetadataTagHelper { Metadata = Metadata(), CurrentPage = 2 });

        html.ShouldContain("<link href=\"https://acme.example/blog?page=2\" rel=\"canonical\">");
        html.ShouldContain("<meta content=\"https://acme.example/blog?page=2\" property=\"og:url\">");
        html.ShouldContain("<link href=\"https://acme.example/blog?page=2\" hreflang=\"en\" rel=\"alternate\">");
        html.ShouldContain("<link href=\"https://acme.example/ja/blog?page=2\" hreflang=\"ja\" rel=\"alternate\">");
        html.ShouldContain("<link href=\"https://acme.example/blog?page=2\" hreflang=\"x-default\" rel=\"alternate\">");
    }

    [Fact]
    public void Page_One_Should_Carry_No_Page_Number()
    {
        var html = Render(new SiteHeadMetadataTagHelper { Metadata = Metadata(), CurrentPage = 1 });

        html.ShouldNotContain("page=");
    }

    [Fact]
    public void The_Page_Query_Key_Should_Be_The_Hosts_Own()
    {
        var html = Render(new SiteHeadMetadataTagHelper { Metadata = Metadata(), CurrentPage = 3, PageQueryKey = "p" });

        html.ShouldContain("<link href=\"https://acme.example/blog?p=3\" rel=\"canonical\">");
    }

    [Fact]
    public void Values_Should_Be_Attribute_Encoded()
    {
        var metadata = Metadata();
        metadata.MetaTitle = "Tips & \"tricks\"";

        var html = Render(new SiteHeadMetadataTagHelper { Metadata = metadata });

        html.ShouldContain("content=\"Tips &amp; &quot;tricks&quot;\"");
    }

    [Fact]
    public void The_Element_Itself_Should_Not_Be_Written()
    {
        var html = Render(new SiteHeadMetadataTagHelper { Metadata = Metadata() });

        html.ShouldNotContain("site-head-metadata");
    }

    private static string Render(SiteHeadMetadataTagHelper tagHelper)
    {
        var context = new TagHelperContext(
            new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "site-head-metadata",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        tagHelper.Process(context, output);

        using var writer = new System.IO.StringWriter();
        output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }
}
