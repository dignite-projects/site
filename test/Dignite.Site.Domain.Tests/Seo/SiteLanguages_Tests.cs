using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// The language rules shared by the Domain and the rendering side (GitHub issue #75): parsing
/// <c>Site.EnabledLanguages</c>, and the two directions of the culture prefix. <c>SiteUrlContext_Tests</c>
/// covers the same prefix rules through the Domain's own entry point.
/// </summary>
public class SiteLanguages_Tests
{
    private static SiteLanguages Languages() => SiteLanguages.Parse("en,zh-Hans,fr");

    [Fact]
    public void Parse_Should_Keep_The_Configured_Order_With_The_First_As_Default()
    {
        var languages = SiteLanguages.Parse("fr, en , zh-Hans");

        languages.EnabledCultureNames.ShouldBe(new[] { "fr", "en", "zh-Hans" });
        languages.DefaultCultureName.ShouldBe("fr");
    }

    [Fact]
    public void Parse_Should_Normalize_And_Deduplicate()
    {
        SiteLanguages.Parse("EN,en,ZH-hans").EnabledCultureNames.ShouldBe(new[] { "en", "zh-Hans" });
    }

    [Fact]
    public void Parse_Should_Skip_Entries_That_Are_Not_Real_Cultures()
    {
        SiteLanguages.Parse("en,not-a-culture-at-all,fr").EnabledCultureNames.ShouldBe(new[] { "en", "fr" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,,")]
    [InlineData("not-a-culture-at-all")]
    public void Parse_Should_Fall_Back_To_English_When_Nothing_Is_Left(string? configured)
    {
        var languages = SiteLanguages.Parse(configured);

        languages.EnabledCultureNames.ShouldBe(new[] { SiteLanguages.FallbackCultureName });
        languages.DefaultCultureName.ShouldBe("en");
    }

    [Theory]
    [InlineData("/", "en", "/")]
    [InlineData("/about", "en", "/about")]
    [InlineData("/", "zh-Hans", "/zh-Hans")]
    [InlineData("/about", "zh-Hans", "/zh-Hans/about")]
    [InlineData("about/", "fr", "/fr/about")]
    public void Apply_Should_Prefix_Every_Language_But_The_Default(string path, string culture, string expected)
    {
        Languages().ApplyCulturePrefix(path, culture).ShouldBe(expected);
    }

    [Theory]
    [InlineData("/", "zh-Hans")]
    [InlineData("/about", "zh-Hans")]
    [InlineData("/blog/my-post", "fr")]
    public void Strip_Should_Undo_Apply(string path, string culture)
    {
        var languages = Languages();

        var prefixed = languages.ApplyCulturePrefix(path, culture);

        languages.TryStripCulturePrefix(prefixed, out var strippedCulture, out var strippedPath).ShouldBeTrue();
        strippedCulture.ShouldBe(culture);
        strippedPath.ShouldBe(path);
    }

    /// <summary>The default language has no prefix to strip: its paths come back as they are.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/about")]
    public void The_Default_Language_Should_Round_Trip_Unprefixed(string path)
    {
        var languages = Languages();

        var applied = languages.ApplyCulturePrefix(path, "en");

        languages.TryStripCulturePrefix(applied, out var culture, out var remaining).ShouldBeFalse();
        culture.ShouldBe("en");
        remaining.ShouldBe(path);
    }

    /// <summary>
    /// "de" is a real culture, but not one this site serves: /de/about is a page route, not a German prefix -
    /// and it is exactly the unprefixed path the router then sees.
    /// </summary>
    [Fact]
    public void A_Culture_The_Site_Does_Not_Serve_Should_Not_Be_Stripped()
    {
        Languages().TryStripCulturePrefix("/de/about", out var culture, out var path).ShouldBeFalse();

        culture.ShouldBe("en");
        path.ShouldBe("/de/about");
        Languages().IsServed("de").ShouldBeFalse();
    }

    /// <summary>
    /// A page whose route is literally <c>/en</c>: "en" is the default language here, so it is never a
    /// prefix, and the page keeps its own URL in both directions.
    /// </summary>
    [Fact]
    public void A_Route_Named_After_The_Default_Language_Should_Stay_A_Route()
    {
        var languages = Languages();

        languages.TryStripCulturePrefix("/en", out var culture, out var path).ShouldBeFalse();
        culture.ShouldBe("en");
        path.ShouldBe("/en");

        languages.ApplyCulturePrefix("/en", "fr").ShouldBe("/fr/en");
        languages.TryStripCulturePrefix("/fr/en", out culture, out path).ShouldBeTrue();
        culture.ShouldBe("fr");
        path.ShouldBe("/en");
    }

    [Fact]
    public void Strip_Should_Normalize_The_Culture_Casing()
    {
        Languages().TryStripCulturePrefix("/ZH-HANS/about", out var culture, out var path).ShouldBeTrue();

        culture.ShouldBe("zh-Hans");
        path.ShouldBe("/about");
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("FR", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("de", false)]
    [InlineData("not-a-culture-at-all", false)]
    [InlineData(null, false)]
    public void IsServed_Should_Answer_For_Enabled_Languages_Only(string? culture, bool expected)
    {
        Languages().IsServed(culture).ShouldBe(expected);
    }
}
