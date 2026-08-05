using Shouldly;
using Xunit;

namespace Dignite.Sites.Seo;

/// <summary>
/// The URL contract every derived document depends on: absolute, on the primary domain, and carrying the
/// language segment only where it belongs (总体设计 §4.2, §5.5).
/// <para>
/// The round-trip assertions are the ones that matter most. Build and strip live in one class precisely so
/// they cannot drift apart, and a URL the sitemap advertises but the router cannot take apart again is a
/// self-inflicted 404 - so they are tested against each other, not just individually.
/// </para>
/// </summary>
public class SiteUrlContext_Tests
{
    private static SiteUrlContext Create(string baseUrl = "https://acme.example")
    {
        return new SiteUrlContext(baseUrl, "en", new[] { "en", "zh-Hans", "fr" });
    }

    [Theory]
    [InlineData("/", "https://acme.example/")]
    [InlineData("/about", "https://acme.example/about")]
    [InlineData("/blog/my-post", "https://acme.example/blog/my-post")]
    public void Default_Language_Should_Carry_No_Prefix(string path, string expected)
    {
        Create().BuildAbsolute(path, "en").ShouldBe(expected);
    }

    [Theory]
    [InlineData("/", "zh-Hans", "https://acme.example/zh-Hans")]
    [InlineData("/about", "zh-Hans", "https://acme.example/zh-Hans/about")]
    [InlineData("/blog/my-post", "fr", "https://acme.example/fr/blog/my-post")]
    public void Other_Languages_Should_Be_Prefixed(string path, string culture, string expected)
    {
        Create().BuildAbsolute(path, culture).ShouldBe(expected);
    }

    /// <summary>
    /// The site root has to stay one URL. Prefixing it naively yields "/zh-Hans/", a second address for
    /// the same page - which is the duplicate content the canonical work exists to avoid.
    /// </summary>
    [Fact]
    public void Root_Should_Not_Gain_A_Trailing_Slash_When_Prefixed()
    {
        Create().ApplyCulturePrefix("/", "zh-Hans").ShouldBe("/zh-Hans");
    }

    [Theory]
    [InlineData("https://acme.example/")]
    [InlineData("https://acme.example")]
    public void Base_Url_Should_Be_Normalized(string baseUrl)
    {
        Create(baseUrl).BuildAbsolute("/about", "en").ShouldBe("https://acme.example/about");
    }

    /// <summary>A site hosted under a path prefix keeps it - the path is part of the site's root.</summary>
    [Fact]
    public void Base_Path_Should_Be_Preserved()
    {
        Create("https://acme.example/site").BuildAbsolute("/about", "zh-Hans")
            .ShouldBe("https://acme.example/site/zh-Hans/about");
    }

    /// <summary>
    /// Slugs deliberately keep Unicode letters (总体设计 §8.3), so a sitemap's <c>&lt;loc&gt;</c> has to
    /// receive them percent-encoded to be a legal URL.
    /// </summary>
    [Fact]
    public void Unicode_Slugs_Should_Be_Percent_Encoded()
    {
        Create().BuildAbsolute("/blog/我的旅行", "en")
            .ShouldBe("https://acme.example/blog/%E6%88%91%E7%9A%84%E6%97%85%E8%A1%8C");
    }

    [Theory]
    [InlineData("/about", "zh-Hans")]
    [InlineData("/blog/my-post", "fr")]
    [InlineData("/", "zh-Hans")]
    public void Stripping_Should_Undo_Building(string path, string culture)
    {
        var context = Create();

        var prefixed = context.ApplyCulturePrefix(path, culture);

        context.TryStripCulturePrefix(prefixed, out var strippedCulture, out var strippedPath).ShouldBeTrue();
        strippedCulture.ShouldBe(culture);
        strippedPath.ShouldBe(path);
    }

    [Fact]
    public void Stripping_An_Unprefixed_Path_Should_Yield_The_Default_Language()
    {
        var context = Create();

        context.TryStripCulturePrefix("/blog/my-post", out var culture, out var path).ShouldBeFalse();

        // Both out values still have to be usable: an unprefixed URL means "default language, path as-is".
        culture.ShouldBe("en");
        path.ShouldBe("/blog/my-post");
    }

    /// <summary>
    /// A page whose route happens to look like a language. "en" resolves as a culture, so a naive strip
    /// would turn this page's own URL into the site root - but it is the default language and therefore
    /// never a prefix, so nothing is stripped.
    /// </summary>
    [Fact]
    public void A_Page_Route_Matching_The_Default_Language_Should_Not_Be_Stripped()
    {
        var context = Create();

        context.TryStripCulturePrefix("/en/team", out _, out var path).ShouldBeFalse();
        path.ShouldBe("/en/team");
    }

    /// <summary>
    /// The same protection for languages this site does not serve: "de" is a real culture, but it is not
    /// enabled here, so /de is a page route rather than a German prefix.
    /// </summary>
    [Fact]
    public void A_Culture_The_Site_Does_Not_Serve_Should_Not_Be_Stripped()
    {
        var context = Create();

        context.TryStripCulturePrefix("/de/about", out _, out var path).ShouldBeFalse();
        path.ShouldBe("/de/about");
    }

    [Fact]
    public void A_Segment_That_Is_Not_A_Culture_Should_Not_Be_Stripped()
    {
        var context = Create();

        context.TryStripCulturePrefix("/blog/my-post", out _, out var path).ShouldBeFalse();
        path.ShouldBe("/blog/my-post");
    }

    /// <summary>Casing is normalized on the way in, the same as everywhere else a culture is read.</summary>
    [Fact]
    public void Stripping_Should_Normalize_The_Culture_Casing()
    {
        var context = Create();

        context.TryStripCulturePrefix("/ZH-HANS/about", out var culture, out var path).ShouldBeTrue();
        culture.ShouldBe("zh-Hans");
        path.ShouldBe("/about");
    }

    [Fact]
    public void A_Bare_Language_Prefix_Should_Resolve_To_The_Site_Root()
    {
        var context = Create();

        context.TryStripCulturePrefix("/zh-Hans", out var culture, out var path).ShouldBeTrue();
        culture.ShouldBe("zh-Hans");
        path.ShouldBe("/");
    }

    /// <summary>An unrecognized culture cannot invent a prefix - it falls back to no prefix at all.</summary>
    [Fact]
    public void An_Unrecognized_Culture_Should_Not_Produce_A_Prefix()
    {
        Create().ApplyCulturePrefix("/about", "not-a-culture-at-all").ShouldBe("/about");
    }
}
