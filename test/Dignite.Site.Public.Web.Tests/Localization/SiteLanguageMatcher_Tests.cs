using Dignite.Site.Seo;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

public class SiteLanguageMatcher_Tests
{
    [Theory]
    [InlineData("ja", "ja")]
    [InlineData("JA", "ja")]
    [InlineData("ja-JP", "ja")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("en-US", "en")]
    public void Should_Match_Through_The_Parent_Chain(string tag, string expected)
    {
        SiteLanguageMatcher.Match(SiteLanguages.Parse("en,ja,zh-Hans,zh-Hant"), tag).ShouldBe(expected);
    }

    /// <summary>A bare root, or a sibling region, takes the first language sharing that root.</summary>
    [Theory]
    [InlineData("zh", "zh-Hant")]
    [InlineData("en-US", "en-GB")]
    public void Should_Fall_Back_To_A_Language_Sharing_The_Root(string tag, string expected)
    {
        SiteLanguageMatcher.Match(SiteLanguages.Parse("ja,zh-Hant,zh-Hans,en-GB"), tag).ShouldBe(expected);
    }

    /// <summary>
    /// The root fallback never crosses scripts: Traditional Chinese is no match for a site serving only
    /// Simplified, nor the other way round - the default language serves such a reader better.
    /// </summary>
    [Theory]
    [InlineData("zh-TW", "en,zh-Hans")]
    [InlineData("zh-Hant", "en,zh-Hans")]
    [InlineData("zh-CN", "en,zh-Hant")]
    [InlineData("sr-Latn-RS", "en,sr-Cyrl")]
    public void Should_Not_Fall_Back_Across_Scripts(string tag, string enabledLanguages)
    {
        SiteLanguageMatcher.Match(SiteLanguages.Parse(enabledLanguages), tag).ShouldBeNull();
    }

    /// <summary>Within one script the root fallback still works: zh-MO reaches zh-HK through zh-Hant.</summary>
    [Fact]
    public void Should_Fall_Back_Within_A_Script()
    {
        SiteLanguageMatcher.Match(SiteLanguages.Parse("en,zh-HK"), "zh-MO").ShouldBe("zh-HK");
    }

    [Theory]
    [InlineData("de")]
    [InlineData("*")]
    [InlineData("not-a-culture-at-all")]
    [InlineData(null)]
    public void Should_Not_Match_Anything_Else(string? tag)
    {
        SiteLanguageMatcher.Match(SiteLanguages.Parse("en,ja"), tag).ShouldBeNull();
    }
}
