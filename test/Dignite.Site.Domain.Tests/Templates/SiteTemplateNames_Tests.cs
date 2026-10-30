using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.Templates;

public class SiteTemplateNames_Tests
{
    [Theory]
    [InlineData("partials/card", "partials/card")]
    [InlineData("/partials/card", "partials/card")]
    [InlineData("  /assets/site.css  ", "assets/site.css")]
    [InlineData("Blog/Post", "Blog/Post")]
    public void Should_Normalize_By_Trimming_And_Dropping_A_Leading_Slash(string input, string expected)
    {
        SiteTemplateNames.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("partials/post-card", true, false, false)]
    [InlineData("partials/blog/card", true, false, false)]
    [InlineData("assets/site.css", false, true, false)]
    [InlineData("assets/js/site.js", false, true, false)]
    [InlineData("Default", false, false, true)]
    [InlineData("Blog/Post", false, false, true)]
    [InlineData("layout", false, false, true)]
    public void Should_Classify_A_Name_By_Its_Prefix(string name, bool partial, bool asset, bool page)
    {
        SiteTemplateNames.IsPartial(name).ShouldBe(partial);
        SiteTemplateNames.IsAsset(name).ShouldBe(asset);
        SiteTemplateNames.IsPage(name).ShouldBe(page);
    }

    [Theory]
    [InlineData("partials/")]
    [InlineData("assets/")]
    public void Should_Not_Treat_The_Bare_Prefix_As_A_Partial_Or_An_Asset(string name)
    {
        // Nothing follows the prefix, so there is no partial or file to speak of; it falls to a page name.
        SiteTemplateNames.IsPartial(name).ShouldBeFalse();
        SiteTemplateNames.IsAsset(name).ShouldBeFalse();
        SiteTemplateNames.IsPage(name).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Partials/card")]
    [InlineData("Assets/site.css")]
    [InlineData("my-partials/card")]
    public void Should_Match_Prefixes_Exactly_And_In_Lower_Case(string name)
    {
        SiteTemplateNames.IsPartial(name).ShouldBeFalse();
        SiteTemplateNames.IsAsset(name).ShouldBeFalse();
        SiteTemplateNames.IsPage(name).ShouldBeTrue();
    }

    [Theory]
    [InlineData("assets/site.css", "site.css")]
    [InlineData("assets/js/site.js", "js/site.js")]
    public void Should_Round_Trip_An_Asset_Name_And_Its_Path(string name, string path)
    {
        SiteTemplateNames.GetAssetPath(name).ShouldBe(path);
        SiteTemplateNames.GetAssetName(path).ShouldBe(name);
    }

    [Fact]
    public void Should_Normalize_The_Path_When_Building_An_Asset_Name()
    {
        SiteTemplateNames.GetAssetName("/site.css").ShouldBe("assets/site.css");
    }

    [Theory]
    [InlineData("partials/card")]
    [InlineData("Default")]
    [InlineData("assets/")]
    public void Should_Not_Give_An_Asset_Path_For_A_Name_That_Is_Not_An_Asset(string name)
    {
        Should.Throw<ArgumentException>(() => SiteTemplateNames.GetAssetPath(name));
    }
}
