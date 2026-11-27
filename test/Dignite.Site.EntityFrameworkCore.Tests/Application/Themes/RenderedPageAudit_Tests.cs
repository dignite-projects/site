using System.Linq;
using Dignite.Site.Admin.Themes;
using Dignite.Site.Themes;
using Shouldly;
using Xunit;

namespace Dignite.Site.Application.Themes;

/// <summary>What a rendered page lacks of what search engines, AI assistants and link previews read from it.</summary>
public class RenderedPageAudit_Tests
{
    /// <summary>A page with everything: the layout a new theme starts with, a heading, and the tags site_head writes.</summary>
    public const string CompletePage =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>Opening hours | Example Bakery</title>
            <meta name="description" content="Open every day from 7 to 19." />
            <meta property="og:image" content="https://example.com/share.jpg" />
            <script type="application/ld+json">{"@context":"https://schema.org","@type":"Bakery","name":"Example Bakery"}</script>
        </head>
        <body>
            <h1>Opening hours</h1>
            <img src="/front.jpg" alt="The shop front" />
            <img src="/divider.svg" alt="" />
            <a href="/contact">Contact</a>
        </body>
        </html>
        """;

    [Fact]
    public void A_Complete_Page_Should_Lack_Nothing()
    {
        RenderedPageAudit.Find(CompletePage, isHomePage: true).ShouldBeEmpty();
    }

    [Fact]
    public void A_Bare_Fragment_Should_Lack_Everything_In_Order()
    {
        RenderedPageAudit.Find("<p>Hello</p>", isHomePage: true)
            .Select(finding => finding.Code)
            .ShouldBe(RenderedPageAudit.Codes.Where(code =>
                code != SiteThemeIssueCodes.ImageAltMissing && code != SiteThemeIssueCodes.LinkTextMissing));
    }

    /// <summary>Only the home page is asked for structured data.</summary>
    [Fact]
    public void Structured_Data_Should_Be_Asked_Of_The_Home_Page_Only()
    {
        var page = CompletePage.Replace("<script type=\"application/ld+json\">", "<script type=\"text/plain\">");

        RenderedPageAudit.Find(page, isHomePage: false).ShouldBeEmpty();
        RenderedPageAudit.Find(page, isHomePage: true).ShouldHaveSingleItem().Code
            .ShouldBe(SiteThemeIssueCodes.StructuredDataMissing);
    }

    [Theory]
    [InlineData("<title> </title>", SiteThemeIssueCodes.TitleMissing)]
    [InlineData("<meta name=\"description\" content=\"\" />", SiteThemeIssueCodes.DescriptionMissing)]
    [InlineData("<meta name=\"viewport\" content=\" \" />", SiteThemeIssueCodes.ViewportMissing)]
    [InlineData("<meta property=\"og:image\" content=\"\" />", SiteThemeIssueCodes.ShareImageMissing)]
    public void A_Tag_With_Nothing_In_It_Should_Count_As_Missing(string tag, string code)
    {
        var page = Strip(CompletePage, code).Replace("<head>", "<head>" + tag);

        RenderedPageAudit.Find(page, isHomePage: false).ShouldHaveSingleItem().Code.ShouldBe(code);
    }

    [Fact]
    public void A_Document_Without_A_Language_Should_Be_Found()
    {
        RenderedPageAudit.Find(CompletePage.Replace("<html lang=\"en\">", "<html>"), isHomePage: false)
            .ShouldHaveSingleItem().Code.ShouldBe(SiteThemeIssueCodes.LanguageMissing);
    }

    /// <summary>A heading hidden from readers is not the page's heading; one marked up with ARIA is.</summary>
    [Fact]
    public void A_Page_Without_A_Visible_H1_Should_Be_Found()
    {
        var hidden = CompletePage.Replace("<h1>Opening hours</h1>", "<div hidden><h1>Opening hours</h1></div>");
        var aria = CompletePage.Replace("<h1>Opening hours</h1>", "<div role=\"heading\" aria-level=\"1\">Opening hours</div>");

        RenderedPageAudit.Find(hidden, isHomePage: false).ShouldHaveSingleItem().Code.ShouldBe(SiteThemeIssueCodes.HeadingMissing);
        RenderedPageAudit.Find(aria, isHomePage: false).ShouldBeEmpty();
    }

    /// <summary>
    /// axe-core's image-alt: an image needs an alt, which may be empty, or a name from ARIA or title, unless it is
    /// presentation or hidden. The finding names the first image without one.
    /// </summary>
    [Theory]
    [InlineData("<img src=\"/a.jpg\" alt=\"\" />", null)]
    [InlineData("<img src=\"/a.jpg\" role=\"presentation\" />", null)]
    [InlineData("<img src=\"/a.jpg\" aria-hidden=\"true\" />", null)]
    [InlineData("<img src=\"/a.jpg\" aria-label=\"A\" />", null)]
    [InlineData("<img src=\"/a.jpg\" title=\"A\" />", null)]
    [InlineData("<img src=\"/a.jpg\" /><img src=\"/b.jpg\" />", "/a.jpg")]
    [InlineData("<img src=\"/a.jpg\" aria-label=\" \" />", "/a.jpg")]
    public void An_Image_Without_Alternative_Text_Should_Be_Found(string images, string? first)
    {
        var findings = RenderedPageAudit.Find(CompletePage.Replace("<body>", "<body>" + images), isHomePage: false);

        if (first == null)
        {
            findings.ShouldBeEmpty();
        }
        else
        {
            var finding = findings.ShouldHaveSingleItem();
            finding.Code.ShouldBe(SiteThemeIssueCodes.ImageAltMissing);
            finding.Element.ShouldBe(first);
        }
    }

    /// <summary>axe-core's link-name: a link's text, the alt of an image in it, or a name from ARIA or title.</summary>
    [Theory]
    [InlineData("<a href=\"/a\"><svg><title>Home</title></svg></a>", null)]
    [InlineData("<a href=\"/a\"><img src=\"/logo.svg\" alt=\"Example Bakery\" /></a>", null)]
    [InlineData("<a href=\"/a\" aria-label=\"Instagram\"><svg aria-hidden=\"true\"></svg></a>", null)]
    [InlineData("<a href=\"/a\" title=\"Instagram\"><i class=\"icon\"></i></a>", null)]
    [InlineData("<span id=\"l\">Map</span><a href=\"/a\" aria-labelledby=\"l\"></a>", null)]
    [InlineData("<a href=\"/a\" aria-hidden=\"true\" tabindex=\"-1\"></a>", null)]
    [InlineData("<a name=\"top\"></a>", null)]
    [InlineData("<a href=\"/a\"> </a><a href=\"/b\"></a>", "/a")]
    [InlineData("<a href=\"/a\"><img src=\"/logo.svg\" alt=\"\" /></a>", "/a")]
    [InlineData("<a href=\"/a\"><span aria-hidden=\"true\">→</span></a>", "/a")]
    [InlineData("<a href=\"/a\" aria-labelledby=\"nothing\"></a>", "/a")]
    public void A_Link_Without_Text_Should_Be_Found(string links, string? first)
    {
        var findings = RenderedPageAudit.Find(CompletePage.Replace("<body>", "<body>" + links), isHomePage: false);

        if (first == null)
        {
            findings.ShouldBeEmpty();
        }
        else
        {
            var finding = findings.ShouldHaveSingleItem();
            finding.Code.ShouldBe(SiteThemeIssueCodes.LinkTextMissing);
            finding.Element.ShouldBe(first);
        }
    }

    [Fact]
    public void A_Long_Address_Should_Be_Cut_In_The_Finding()
    {
        var source = "data:image/png;base64," + new string('A', 500);

        var finding = RenderedPageAudit.Find(CompletePage.Replace("<body>", $"<body><img src=\"{source}\" />"), isHomePage: false)
            .ShouldHaveSingleItem();

        finding.Element!.Length.ShouldBe(RenderedPageAudit.MaxElementLength + 1);
        finding.Element.ShouldEndWith("…");
    }

    [Fact]
    public void Nothing_Should_Be_Found_In_Nothing()
    {
        RenderedPageAudit.Find(null, isHomePage: true).ShouldBeEmpty();
        RenderedPageAudit.Find(" ", isHomePage: true).ShouldBeEmpty();
    }

    /// <summary>The complete page without the tag a code is about.</summary>
    private static string Strip(string page, string code)
    {
        return code switch
        {
            SiteThemeIssueCodes.TitleMissing => page.Replace("<title>Opening hours | Example Bakery</title>", ""),
            SiteThemeIssueCodes.DescriptionMissing => page.Replace("<meta name=\"description\" content=\"Open every day from 7 to 19.\" />", ""),
            SiteThemeIssueCodes.ViewportMissing => page.Replace("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />", ""),
            SiteThemeIssueCodes.ShareImageMissing => page.Replace("<meta property=\"og:image\" content=\"https://example.com/share.jpg\" />", ""),
            _ => page
        };
    }
}
