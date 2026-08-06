using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.Pages;

public class Page_Tests
{
    private static readonly DateTime PublishTime = new(2026, 7, 15, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// Routes are matched against request paths, so they have to reach the table in one canonical shape -
    /// otherwise "is this route taken?" answers no for a route that is.
    /// </summary>
    [Theory]
    [InlineData("blog", "/blog")]
    [InlineData("/blog", "/blog")]
    [InlineData("/blog/", "/blog")]
    [InlineData("  /blog/  ", "/blog")]
    [InlineData("/", "/")]
    [InlineData("", null)]
    public void Should_Normalize_Route(string input, string? expected)
    {
        if (expected == null)
        {
            Should.Throw<ArgumentException>(() => Page.NormalizeRoute(input));
            return;
        }

        Page.NormalizeRoute(input).ShouldBe(expected);
    }

    [Fact]
    public void Should_Build_Content_Path_Under_Its_Route()
    {
        var page = NewPage("/blog");

        page.BuildContentPath(PublishTime, "my-post").ShouldBe("/blog/my-post");
    }

    [Fact]
    public void Should_Build_Dated_Content_Path()
    {
        var page = NewPage("/news", "{publishTime:yyyy/MM}/{slug}");

        page.BuildContentPath(PublishTime, "my-post").ShouldBe("/news/2026/07/my-post");
    }

    /// <summary>
    /// A page carrying one single content: the content's URL is the page route itself, with no trailing
    /// segment and no trailing slash.
    /// </summary>
    [Fact]
    public void Should_Build_Page_Route_Itself_For_Empty_Slug()
    {
        NewPage("/about").BuildContentPath(PublishTime, "").ShouldBe("/about");
        NewPage("/").BuildContentPath(PublishTime, "").ShouldBe("/");
    }

    /// <summary>
    /// The site root is the one route that is a bare slash, so joining a content path to it must not
    /// produce a doubled separator.
    /// </summary>
    [Fact]
    public void Should_Not_Double_Slash_Under_Root_Page()
    {
        NewPage("/").BuildContentPath(PublishTime, "welcome").ShouldBe("/welcome");
    }

    [Fact]
    public void Should_Reject_Content_Path_Pattern_Without_Slug()
    {
        Should.Throw<InvalidContentPathPatternException>(() => NewPage("/news", "{publishTime:yyyy/MM}"));
    }

    private static Page NewPage(string route, string? contentPathPattern = null)
    {
        return new Page(Guid.NewGuid(), "test-page", "Test Page", route, contentPathPattern);
    }
}
