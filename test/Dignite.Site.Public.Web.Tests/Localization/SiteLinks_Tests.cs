using Dignite.Site.Seo;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary>Localizing in-site links into the current page's language (GitHub issue #75).</summary>
public class SiteLinks_Tests
{
    private static readonly SiteLanguages Languages = SiteLanguages.Parse("en,ja,zh-Hans");

    private static string Localize(string url, string culture) =>
        SiteLinks.Localize(url, culture, Languages, new SiteLanguageOptions().ExcludedPathPrefixes);

    [Theory]
    [InlineData("~/", "~/ja")]
    [InlineData("~/about", "~/ja/about")]
    [InlineData("~/blog/my-post", "~/ja/blog/my-post")]
    [InlineData("~/about/", "~/ja/about")]
    public void Should_Prefix_A_Site_Page(string url, string expected)
    {
        Localize(url, "ja").ShouldBe(expected);
    }

    [Theory]
    [InlineData("~/")]
    [InlineData("~/about")]
    [InlineData("~/about/")]
    [InlineData("~/about?tab=2")]
    public void The_Default_Language_Should_Leave_Links_Untouched(string url)
    {
        Localize(url, "en").ShouldBe(url);
    }

    [Theory]
    [InlineData("~/about?tab=2", "~/ja/about?tab=2")]
    [InlineData("~/about#team", "~/ja/about#team")]
    [InlineData("~/about?tab=2#team", "~/ja/about?tab=2#team")]
    [InlineData("~/?ref=nav", "~/ja?ref=nav")]
    public void Should_Keep_The_Query_And_Fragment(string url, string expected)
    {
        Localize(url, "ja").ShouldBe(expected);
    }

    [Theory]
    [InlineData("~/Account/Login")]
    [InlineData("~/account/login?returnUrl=%2F")]
    [InlineData("~/Abp/Languages/Switch")]
    [InlineData("~/api/site/contents")]
    [InlineData("~/api")]
    [InlineData("~/connect/authorize")]
    [InlineData("~/swagger")]
    [InlineData("~/Error?httpStatusCode=404")]
    [InlineData("~/.well-known/openid-configuration")]
    public void Should_Leave_Excluded_Paths_Untouched(string url)
    {
        Localize(url, "ja").ShouldBe(url);
    }

    /// <summary>Excluded per whole segment: "/apiary" is a page, not the API.</summary>
    [Fact]
    public void Should_Match_Excluded_Paths_By_Whole_Segment()
    {
        Localize("~/apiary", "ja").ShouldBe("~/ja/apiary");
    }

    [Theory]
    [InlineData("~/favicon.ico")]
    [InlineData("~/sitemap.xml")]
    [InlineData("~/files/report.pdf?v=2")]
    public void Should_Leave_File_Like_Paths_Untouched(string url)
    {
        Localize(url, "ja").ShouldBe(url);
    }

    /// <summary>A host's own prefix works however it is written - with or without its slashes.</summary>
    [Theory]
    [InlineData("/Contact")]
    [InlineData("/Contact/")]
    [InlineData("Contact")]
    [InlineData(" contact/ ")]
    public void Should_Honor_A_Hosts_Own_Excluded_Paths(string prefix)
    {
        var options = new SiteLanguageOptions();
        options.ExcludedPathPrefixes.Add(prefix);

        SiteLinks.Localize("~/contact", "ja", Languages, options.ExcludedPathPrefixes).ShouldBe("~/contact");
        SiteLinks.Localize("~/contact/form?x=1", "ja", Languages, options.ExcludedPathPrefixes).ShouldBe("~/contact/form?x=1");
        SiteLinks.Localize("~/contacts", "ja", Languages, options.ExcludedPathPrefixes).ShouldBe("~/ja/contacts");
    }

    /// <summary>A prefix of nothing but slashes would exclude every path; it is ignored instead.</summary>
    [Fact]
    public void An_Empty_Prefix_Should_Exclude_Nothing()
    {
        SiteLinks.Localize("~/about", "ja", Languages, new[] { "/", "", "  " }).ShouldBe("~/ja/about");
    }

    /// <summary>A link written in one language on purpose is not prefixed a second time.</summary>
    [Fact]
    public void Should_Leave_An_Already_Prefixed_Link_Untouched()
    {
        Localize("~/zh-Hans/about", "ja").ShouldBe("~/zh-Hans/about");
    }

    [Theory]
    [InlineData("/about")]
    [InlineData("https://example.com/about")]
    [InlineData("#top")]
    [InlineData("about")]
    public void Should_Leave_Links_That_Are_Not_App_Relative_Untouched(string url)
    {
        Localize(url, "ja").ShouldBe(url);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("not-a-culture-at-all")]
    public void Should_Leave_Links_Untouched_For_A_Language_The_Site_Does_Not_Serve(string culture)
    {
        Localize("~/about", culture).ShouldBe("~/about");
    }
}
