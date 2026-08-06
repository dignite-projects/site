using System;
using Dignite.Site.Pages;
using Shouldly;
using Xunit;

namespace Dignite.Site.Pages;

/// <summary>
/// <see cref="ContentPathPattern"/> is used in both directions - it composes a content's URL when one is
/// emitted, and takes it apart again when a request is routed. The two have to agree exactly, so most of
/// what is worth testing here is a round trip rather than either half alone.
/// </summary>
public class ContentPathPattern_Tests
{
    private static readonly DateTime PublishTime = new(2026, 7, 15, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_Build_Slug_Only_Path_By_Default()
    {
        ContentPathPattern.Build(null, PublishTime, "my-post").ShouldBe("my-post");
        ContentPathPattern.Build("{slug}", PublishTime, "my-post").ShouldBe("my-post");
    }

    [Fact]
    public void Should_Build_Dated_Path()
    {
        ContentPathPattern.Build("{publishTime:yyyy/MM}/{slug}", PublishTime, "my-post")
            .ShouldBe("2026/07/my-post");

        ContentPathPattern.Build("{publishTime:yyyy/MM/dd}/{slug}", PublishTime, "my-post")
            .ShouldBe("2026/07/15/my-post");
    }

    /// <summary>
    /// An empty slug is the single content of a home or "about" page, whose URL is its page route alone.
    /// </summary>
    [Fact]
    public void Should_Build_Empty_Path_For_Empty_Slug()
    {
        ContentPathPattern.Build("{slug}", PublishTime, "").ShouldBe("");
        ContentPathPattern.Build("{publishTime:yyyy/MM}/{slug}", PublishTime, null).ShouldBe("");
    }

    [Theory]
    [InlineData("{slug}", "my-post")]
    [InlineData("{publishTime:yyyy}/{slug}", "my-post")]
    [InlineData("{publishTime:yyyy/MM}/{slug}", "my-post")]
    [InlineData("{publishTime:yyyy/MM/dd}/{slug}", "hello-world-2026")]
    [InlineData("archive/{publishTime:yyyy-MM}/{slug}", "my-post")]
    public void Should_Round_Trip(string pattern, string slug)
    {
        var path = ContentPathPattern.Build(pattern, PublishTime, slug);

        ContentPathPattern.TryExtractSlug(pattern, path, out var extracted).ShouldBeTrue();
        extracted.ShouldBe(slug);
    }

    /// <summary>
    /// The date segment is decoration once it has matched - the content is identified by
    /// (page, culture, slug) alone. Requiring the date to agree with the stored publish time would break
    /// every existing link the moment an editor reschedules a post.
    /// </summary>
    [Fact]
    public void Should_Extract_Slug_Regardless_Of_Which_Date_Matched()
    {
        ContentPathPattern.TryExtractSlug("{publishTime:yyyy/MM}/{slug}", "1999/01/my-post", out var slug)
            .ShouldBeTrue();
        slug.ShouldBe("my-post");
    }

    [Fact]
    public void Should_Not_Match_Path_With_Extra_Segments()
    {
        // {slug} is a single segment, so a dated path must not resolve under the default pattern.
        ContentPathPattern.TryExtractSlug("{slug}", "2026/07/my-post", out _).ShouldBeFalse();

        // ...and the reverse: a bare slug is not a valid dated path.
        ContentPathPattern.TryExtractSlug("{publishTime:yyyy/MM}/{slug}", "my-post", out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Match_Malformed_Date_Segment()
    {
        ContentPathPattern.TryExtractSlug("{publishTime:yyyy/MM}/{slug}", "20xx/07/my-post", out _).ShouldBeFalse();
        ContentPathPattern.TryExtractSlug("{publishTime:yyyy/MM}/{slug}", "2026/7x/my-post", out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Reject_Pattern_Without_Slug_Placeholder()
    {
        ContentPathPattern.IsValid("{publishTime:yyyy/MM}").ShouldBeFalse();
        ContentPathPattern.IsValid("").ShouldBeFalse();
        ContentPathPattern.IsValid(null).ShouldBeFalse();

        ContentPathPattern.IsValid("{slug}").ShouldBeTrue();
        ContentPathPattern.IsValid("{publishTime:yyyy}/{slug}").ShouldBeTrue();
    }

    /// <summary>
    /// A token this class does not understand must fail validation rather than silently becoming literal
    /// text. Without this, {year}/{month}/{slug} - a plausible guess at the placeholder vocabulary, and
    /// exactly what an earlier MCP tool description told a model to write - would pass IsValid, then
    /// Build would bake the literal string "{year}" into every URL, which would never resolve.
    /// </summary>
    [Fact]
    public void Should_Reject_A_Pattern_With_An_Unrecognized_Placeholder()
    {
        ContentPathPattern.IsValid("{year}/{month}/{slug}").ShouldBeFalse();
        ContentPathPattern.IsValid("{slug}/{oops}").ShouldBeFalse();
        ContentPathPattern.IsValid("{publishTim:yyyy}/{slug}").ShouldBeFalse();

        // A stray, unmatched brace is caught the same way - it cannot be a recognized placeholder either.
        ContentPathPattern.IsValid("{slug}/{").ShouldBeFalse();
    }

    /// <summary>
    /// A literal run is escaped before being compiled, so regex metacharacters in a pattern are matched
    /// as themselves rather than reinterpreted.
    /// </summary>
    [Fact]
    public void Should_Treat_Literals_Literally()
    {
        ContentPathPattern.TryExtractSlug("a.b/{slug}", "a.b/my-post", out var slug).ShouldBeTrue();
        slug.ShouldBe("my-post");

        ContentPathPattern.TryExtractSlug("a.b/{slug}", "axb/my-post", out _).ShouldBeFalse();
    }
}
