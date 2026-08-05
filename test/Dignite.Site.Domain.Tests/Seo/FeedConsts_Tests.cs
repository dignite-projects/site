using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// The rule that decides whether a request path is a feed address at all (GitHub issue #31).
/// <para>
/// It carries more weight than its size suggests: the same method gates the route constraint that decides
/// whether the feed endpoint is reached and the service that then serves it. If it were written twice,
/// the two copies could disagree - producing either a path that routes to a handler which then rejects it,
/// or a bare catch-all that swallows the rest of the site.
/// </para>
/// </summary>
public class FeedConsts_Tests
{
    [Theory]
    [InlineData("blog/feed.xml", SiteFeedFormat.Rss, "blog")]
    [InlineData("blog/atom.xml", SiteFeedFormat.Atom, "blog")]
    [InlineData("blog/feed.json", SiteFeedFormat.Json, "blog")]
    [InlineData("/blog/feed.xml", SiteFeedFormat.Rss, "blog")]
    [InlineData("zh-Hans/about/feed.xml", SiteFeedFormat.Rss, "zh-Hans/about")]
    [InlineData("docs/guides/getting-started/atom.xml", SiteFeedFormat.Atom, "docs/guides/getting-started")]
    // The site root's own feed - no page segment at all.
    [InlineData("feed.xml", SiteFeedFormat.Rss, "")]
    [InlineData("/feed.json", SiteFeedFormat.Json, "")]
    public void Should_Split_A_Feed_Path_Into_Format_And_Page(
        string feedPath, SiteFeedFormat expectedFormat, string expectedRemainder)
    {
        FeedConsts.TryParsePath(feedPath, out var format, out var remainder).ShouldBeTrue();

        format.ShouldBe(expectedFormat);
        remainder.ShouldBe(expectedRemainder);
    }

    /// <summary>The last segment is the one part of a feed URL people retype by hand.</summary>
    [Theory]
    [InlineData("blog/FEED.XML")]
    [InlineData("blog/Atom.Xml")]
    public void Should_Accept_Any_Casing_Of_The_File_Name(string feedPath)
    {
        FeedConsts.TryParsePath(feedPath, out _, out _).ShouldBeTrue();
    }

    /// <summary>
    /// Everything else has to fall through, or the catch-all route this gates would take over the site.
    /// </summary>
    [Theory]
    [InlineData("blog/rss.xml")]
    [InlineData("blog")]
    [InlineData("blog/my-post")]
    [InlineData("feed.xml/more")]
    [InlineData("swagger/index.html")]
    [InlineData("")]
    [InlineData("/")]
    [InlineData(null)]
    public void Should_Reject_Anything_That_Is_Not_A_Feed_Address(string? feedPath)
    {
        FeedConsts.TryParsePath(feedPath, out _, out var remainder).ShouldBeFalse();
        remainder.ShouldBeEmpty();
    }

    [Fact]
    public void Every_Format_Should_Round_Trip_Through_Its_File_Name()
    {
        foreach (var format in new[] { SiteFeedFormat.Rss, SiteFeedFormat.Atom, SiteFeedFormat.Json })
        {
            FeedConsts.TryParseFileName(FeedConsts.GetFileName(format), out var parsed).ShouldBeTrue();
            parsed.ShouldBe(format);
        }
    }
}
