using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Preview;

public class SiteTemplatePreviewBanner_Tests
{
    private const string Banner = "<div id=\"b\">$1 $$ ${0}</div>";

    [Theory]
    [InlineData("<html><body><p>x</p></body></html>", "<html><body>" + Banner + "<p>x</p></body></html>")]
    [InlineData("<html><BODY class=\"home\" data-x='1'>x</BODY></html>", "<html><BODY class=\"home\" data-x='1'>" + Banner + "x</BODY></html>")]
    [InlineData("<html><body\nclass=\"a\">x</body></html>", "<html><body\nclass=\"a\">" + Banner + "x</body></html>")]
    public void The_Banner_Should_Follow_The_Opening_Body_Tag(string html, string expected)
    {
        SiteTemplatePreviewBanner.Inject(html, Banner).ShouldBe(expected);
    }

    /// <summary>At the end, not the start: before a doctype it would switch the page to quirks mode.</summary>
    [Fact]
    public void A_Document_Without_A_Body_Tag_Should_Get_The_Banner_At_The_End()
    {
        SiteTemplatePreviewBanner.Inject("<!DOCTYPE html><bodyguard>x</bodyguard>", Banner)
            .ShouldBe("<!DOCTYPE html><bodyguard>x</bodyguard>" + Banner);
    }

    /// <summary>Only the first body tag; the page's own <c>$1</c> and the banner's are left as they are.</summary>
    [Fact]
    public void Only_The_First_Body_Tag_Should_Get_The_Banner()
    {
        SiteTemplatePreviewBanner.Inject("<body>$1</body><body>", Banner).ShouldBe("<body>" + Banner + "$1</body><body>");
    }

    [Fact]
    public void Texts_And_The_Address_Should_Be_Encoded()
    {
        var banner = SiteTemplatePreviewBanner.Build("Previewing <script>", "Exit & go", "/exit?a=\"b\"");

        banner.ShouldStartWith("<div id=\"" + SiteTemplatePreviewConsts.BannerElementId + "\" style=\"position:sticky;");
        banner.ShouldContain("<span>Previewing &lt;script&gt;</span>");
        banner.ShouldContain(">Exit &amp; go</a>");
        banner.ShouldContain("href=\"/exit?a=&quot;b&quot;\"");
        banner.ShouldNotContain("<script>");
    }
}
