using System;
using System.Globalization;
using Dignite.Site.Pages;
using Shouldly;
using Xunit;

namespace Dignite.Site.Pages;

/// <summary>
/// <see cref="PageRoute"/> is used in both directions - it composes a content's URL when one is emitted,
/// and takes it apart again when a request is routed. The two have to agree exactly, so most of what is
/// worth testing here is a round trip rather than either half alone.
/// </summary>
public class PageRoute_Tests
{
    private static readonly DateTime PublishTime = new(2026, 7, 15, 9, 30, 0, DateTimeKind.Utc);

    private static string ResolveSlugAndPublishTime(string name, string? format, string slug)
    {
        return name switch
        {
            "slug" => slug,
            "publishTime" => PublishTime.ToString(format, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unexpected placeholder '{name}' in this test.")
        };
    }

    [Fact]
    public void Should_Build_Slug_Only_Path()
    {
        PageRoute.Build("/blog/{slug}", (name, format) => ResolveSlugAndPublishTime(name, format, "my-post"))
            .ShouldBe("/blog/my-post");
    }

    [Fact]
    public void Should_Build_Dated_Path()
    {
        PageRoute.Build("/news/{publishTime:yyyy-MM}/{slug}", (name, format) => ResolveSlugAndPublishTime(name, format, "my-post"))
            .ShouldBe("/news/2026-07/my-post");

        PageRoute.Build("/news/{publishTime:yyyy-MM-dd}/{slug}", (name, format) => ResolveSlugAndPublishTime(name, format, "my-post"))
            .ShouldBe("/news/2026-07-15/my-post");
    }

    /// <summary>
    /// A placeholder's name is never limited to "slug" or "publishTime" - a route can reference any field
    /// the content has, system property or FlexFields business field alike (总体设计 §2.4). Build does not
    /// know or care which kind "category" is; it only knows to ask the resolver for it.
    /// </summary>
    [Fact]
    public void Should_Build_Using_An_Arbitrary_Field_Name()
    {
        PageRoute.Build("/blog/{category}/{slug}", (name, _) => name switch
            {
                "category" => "travel",
                "slug" => "my-post",
                _ => throw new InvalidOperationException()
            })
            .ShouldBe("/blog/travel/my-post");
    }

    [Theory]
    [InlineData("/blog/{slug}", "my-post")]
    [InlineData("/blog/{publishTime:yyyy}/{slug}", "my-post")]
    [InlineData("/blog/{publishTime:yyyy-MM}/{slug}", "my-post")]
    [InlineData("/blog/{publishTime:yyyy-MM-dd}/{slug}", "hello-world-2026")]
    [InlineData("/blog/archive/{publishTime:yyyy-MM}/{slug}", "my-post")]
    public void Should_Round_Trip(string route, string slug)
    {
        var path = PageRoute.Build(route, (name, format) => ResolveSlugAndPublishTime(name, format, slug));

        PageRoute.TryMatchSlug(route, path, out var extracted).ShouldBeTrue();
        extracted.ShouldBe(slug);
    }

    /// <summary>
    /// The date segment is decoration once it has matched - the content is identified by
    /// (page, culture, slug) alone. Requiring the date to agree with the stored publish time would break
    /// every existing link the moment an editor reschedules a post, and the same reasoning means a
    /// decorative segment is never checked against its own format either - garbage in that position still
    /// resolves, exactly like a stale date does.
    /// </summary>
    [Theory]
    [InlineData("/news/1999-01/my-post")]
    [InlineData("/news/not-a-date/my-post")]
    public void Should_Extract_Slug_Regardless_Of_What_The_Decorative_Segment_Contains(string path)
    {
        PageRoute.TryMatchSlug("/news/{publishTime:yyyy-MM}/{slug}", path, out var slug).ShouldBeTrue();
        slug.ShouldBe("my-post");
    }

    /// <summary>
    /// A slug is exactly one path segment. Without the guard this pins, a route whose last placeholder is
    /// {slug} would let FormattedStringValueExtracter fold every remaining segment into it - see
    /// TryMatchSlug's remarks.
    /// </summary>
    [Fact]
    public void Should_Not_Match_Path_With_Extra_Segments()
    {
        // {slug} is a single segment, so a longer path must not resolve under a slug-only route.
        PageRoute.TryMatchSlug("/blog/{slug}", "/blog/2026/07/my-post", out _).ShouldBeFalse();

        // ...and the reverse: a bare slug is not a valid dated path.
        PageRoute.TryMatchSlug("/news/{publishTime:yyyy-MM}/{slug}", "/news/my-post", out _).ShouldBeFalse();
    }

    /// <summary>
    /// The core behavior change from merging Route and ContentPathPattern: there is no longer an implicit
    /// default that requires {slug} to be present. A route with none is legitimate - that is a page with
    /// nothing beneath it (总体设计 §3.3), not an error.
    /// </summary>
    [Fact]
    public void Should_Allow_A_Route_Without_A_Slug_Placeholder()
    {
        PageRoute.IsValid("/about").ShouldBeTrue();
        PageRoute.IsValid("/news/{publishTime:yyyy-MM}").ShouldBeTrue();

        PageRoute.IsValid("").ShouldBeFalse();
        PageRoute.IsValid(null).ShouldBeFalse();
    }

    /// <summary>
    /// {slug?} allows an empty slug beneath the route in addition to a non-empty one; plain {slug} does
    /// not. The two are mutually exclusive - asking for both at once has no coherent meaning.
    /// </summary>
    [Fact]
    public void Should_Recognize_The_Optional_Slug_Token()
    {
        PageRoute.IsValid("/about/{slug?}").ShouldBeTrue();
        PageRoute.HasSlug("/about/{slug?}").ShouldBeTrue();
        PageRoute.IsSlugOptional("/about/{slug?}").ShouldBeTrue();

        PageRoute.IsSlugOptional("/blog/{slug}").ShouldBeFalse();
        PageRoute.IsSlugOptional("/about").ShouldBeFalse();

        PageRoute.IsValid("/about/{slug}{slug?}").ShouldBeFalse();
    }

    /// <summary>
    /// A route's placeholders are not limited to a fixed vocabulary - {category}, {author}, anything a
    /// content type actually declares is syntactically fine here, because whether the name resolves to a
    /// real field is a schema-aware question this class (Domain.Shared, no repository access) cannot ask.
    /// That check happens where the content is actually read - see Page.BuildContentPath - not here.
    /// </summary>
    [Fact]
    public void Should_Accept_A_Route_Whose_Placeholder_Names_Are_Not_Slug_Or_PublishTime()
    {
        PageRoute.IsValid("/news/{year}/{month}/{slug}").ShouldBeTrue();
        PageRoute.IsValid("/blog/{category}/{slug}").ShouldBeTrue();
    }

    /// <summary>
    /// A stray, unmatched brace can never be a well-formed placeholder, whatever vocabulary is allowed for
    /// names.
    /// </summary>
    [Fact]
    public void Should_Reject_A_Route_With_An_Unmatched_Brace()
    {
        PageRoute.IsValid("/blog/{slug}/{").ShouldBeFalse();
        PageRoute.IsValid("/blog/{slug}}").ShouldBeFalse();
    }

    /// <summary>
    /// A placeholder's ":FORMAT" suffix is restricted to letters, digits, '.', '_', '-' - never '/',
    /// which FormattedStringValueExtracter would be unable to tell apart from the '/' that separates one
    /// placeholder from the next (see the class remarks). This is not a rule about dates specifically: it
    /// is checked the same way for any placeholder name.
    /// </summary>
    [Fact]
    public void Should_Reject_A_Format_Containing_A_Slash_Regardless_Of_The_Placeholder_Name()
    {
        PageRoute.IsValid("/news/{publishTime:yyyy/MM}/{slug}").ShouldBeFalse();
        PageRoute.IsValid("/news/{category:yyyy/MM}/{slug}").ShouldBeFalse();

        PageRoute.IsValid("/news/{publishTime:yyyy-MM}/{slug}").ShouldBeTrue();
        PageRoute.IsValid("/news/{publishTime:yyyy.MM_dd}/{slug}").ShouldBeTrue();
    }

    /// <summary>
    /// A literal run is matched as plain text, not compiled into a regex - so a character that would be a
    /// metacharacter if it were (like '.') still means only itself.
    /// </summary>
    [Fact]
    public void Should_Treat_Literals_Literally()
    {
        PageRoute.TryMatchSlug("/a.b/{slug}", "/a.b/my-post", out var slug).ShouldBeTrue();
        slug.ShouldBe("my-post");

        PageRoute.TryMatchSlug("/a.b/{slug}", "/axb/my-post", out _).ShouldBeFalse();
    }

    /// <summary>
    /// The page's own address is everything up to the segment that first carries a placeholder - what
    /// sitemap, canonical, hreflang and feed URLs are built from (<see cref="Page.GetPath"/>), never the
    /// raw route.
    /// </summary>
    [Theory]
    [InlineData("/about", "/about")]
    [InlineData("/", "/")]
    [InlineData("/blog/{slug}", "/blog")]
    [InlineData("/about/{slug?}", "/about")]
    [InlineData("/{slug}", "/")]
    [InlineData("/blog/post-{slug}", "/blog")]
    [InlineData("/news/{publishTime:yyyy-MM}/{slug}", "/news")]
    public void Should_Derive_The_Pages_Own_Path(string route, string expectedPath)
    {
        PageRoute.GetPath(route).ShouldBe(expectedPath);
    }

    [Fact]
    public void Should_Report_Whether_A_Route_Has_A_Slug()
    {
        PageRoute.HasSlug("/blog/{slug}").ShouldBeTrue();
        PageRoute.HasSlug("/about/{slug?}").ShouldBeTrue();
        PageRoute.HasSlug("/about").ShouldBeFalse();
    }

    /// <summary>
    /// The one place "is this a template or a plain literal path" is decided - the literal-wins tie-break
    /// (总体设计 §3.4) reads this, not its own ad hoc '{' check, so a literal and a template sharing an
    /// address never disagree with GetPath about which one is which.
    /// </summary>
    [Fact]
    public void Should_Report_Whether_A_Route_Is_A_Template()
    {
        PageRoute.IsTemplate("/blog/{slug}").ShouldBeTrue();
        PageRoute.IsTemplate("/about/{slug?}").ShouldBeTrue();
        PageRoute.IsTemplate("/about").ShouldBeFalse();
        PageRoute.IsTemplate("/").ShouldBeFalse();
    }

    /// <summary>
    /// {slug}/{slug?} must be a route's last placeholder - what makes "the placeholders before slug" in
    /// TryMatchPartial a well-defined, contiguous prefix to cut at. Every route in this repository already
    /// puts slug last, so this only closes off a shape nothing legitimately used.
    /// </summary>
    [Fact]
    public void Should_Reject_A_Route_Where_Slug_Is_Not_The_Last_Placeholder()
    {
        PageRoute.IsValid("/blog/{slug}/{publishTime:yyyy-MM}").ShouldBeFalse();
        PageRoute.IsValid("/blog/{slug?}/{publishTime:yyyy-MM}").ShouldBeFalse();

        PageRoute.IsValid("/blog/{publishTime:yyyy-MM}/{slug}").ShouldBeTrue();
        PageRoute.IsValid("/blog/{publishTime:yyyy-MM}/{slug?}").ShouldBeTrue();
    }

    /// <summary>
    /// TryMatchPartial answers what the placeholders before slug mean when a path only names some leading
    /// subset of them - deepest cut (every placeholder but slug) down to shallowest (just the first).
    /// </summary>
    [Fact]
    public void Should_Match_Every_Prefix_Cut_Short_Of_Slug()
    {
        const string route = "/blog/abc/{cultureName}/{publishTime:yyyy-MM}/{slug}";

        PageRoute.TryMatchPartial(route, "/blog/abc/en/2026-08", out var both).ShouldBeTrue();
        both.Count.ShouldBe(2);
        both["cultureName"].ShouldBe("en");
        both["publishTime:yyyy-MM"].ShouldBe("2026-08");

        PageRoute.TryMatchPartial(route, "/blog/abc/en", out var one).ShouldBeTrue();
        one.Count.ShouldBe(1);
        one["cultureName"].ShouldBe("en");
    }

    /// <summary>
    /// Slug is never one of the placeholders TryMatchPartial fills - a path with a value for every
    /// placeholder, slug included, is TryMatchSlug's full match, not a partial one.
    /// </summary>
    [Fact]
    public void Should_Not_Treat_A_Full_Match_As_Partial()
    {
        PageRoute.TryMatchPartial(
            "/blog/{cultureName}/{publishTime:yyyy-MM}/{slug}", "/blog/en/2026-08/my-post", out _)
            .ShouldBeFalse();
    }

    /// <summary>
    /// The bare address (nothing beyond the route's own literal prefix) is GetPath/FindByPathAsync's
    /// territory, not TryMatchPartial's - it only ever fills at least one placeholder.
    /// </summary>
    [Fact]
    public void Should_Not_Match_The_Bare_Address()
    {
        PageRoute.TryMatchPartial("/blog/{cultureName}/{publishTime:yyyy-MM}/{slug}", "/blog", out _)
            .ShouldBeFalse();
    }

    /// <summary>
    /// A route where slug is the only placeholder has nothing before it to cut - {slug?} included, since
    /// canonicalizing it to {slug} still leaves nothing else.
    /// </summary>
    [Fact]
    public void Should_Not_Match_When_Slug_Is_The_Only_Placeholder()
    {
        PageRoute.TryMatchPartial("/blog/{slug}", "/blog/my-post", out _).ShouldBeFalse();
        PageRoute.TryMatchPartial("/about/{slug?}", "/about/my-post", out _).ShouldBeFalse();
    }

    /// <summary>
    /// A shallower cut's own last placeholder is exactly as unbounded as TryMatchSlug's slug is when
    /// nothing follows it - without rejecting a captured value that contains '/', "cultureName" alone
    /// would swallow "en/2026-08" whole instead of the deeper, correctly-scoped cut winning.
    /// </summary>
    [Fact]
    public void Should_Not_Let_A_Shallower_Cut_Swallow_A_Deeper_Ones_Segments()
    {
        PageRoute.TryMatchPartial(
            "/blog/{cultureName}/{publishTime:yyyy-MM}/{slug}", "/blog/en/2026-08", out var values)
            .ShouldBeTrue();

        values.Count.ShouldBe(2);
        values["cultureName"].ShouldBe("en");
    }

    /// <summary>
    /// Mirrors Should_Extract_Slug_Regardless_Of_What_The_Decorative_Segment_Contains:
    /// FormattedStringValueExtracter never validates a captured value against its own :FORMAT - "launch"
    /// fills publishTime:yyyy-MM exactly as readily as a real month would, garbage and all. Whether that
    /// value is usable is left to whatever consumes FilterValues, the same way a decorative segment in a
    /// full match is never checked either.
    /// </summary>
    [Fact]
    public void Should_Fill_A_Placeholder_Regardless_Of_Whether_The_Value_Matches_Its_Format()
    {
        PageRoute.TryMatchPartial("/news/{publishTime:yyyy-MM}/{slug}", "/news/launch", out var values)
            .ShouldBeTrue();

        values["publishTime:yyyy-MM"].ShouldBe("launch");
    }

    /// <summary>
    /// A cut's template used to stop right at the end of its own last placeholder, silently dropping any
    /// literal text between it and the next (unfilled) placeholder - "-archive" here. That let a path
    /// missing that literal text match anyway, with the literal separator effectively optional.
    /// </summary>
    [Fact]
    public void Should_Require_The_Literal_Text_Between_A_Cut_Placeholder_And_The_Next_One()
    {
        const string route = "/blog/{category}-archive/{publishTime:yyyy-MM}/{slug}";

        PageRoute.TryMatchPartial(route, "/blog/travel-archive/2026-08", out var values).ShouldBeTrue();
        values["category"].ShouldBe("travel");
        values["publishTime:yyyy-MM"].ShouldBe("2026-08");

        // Without "-archive", this must not fall back to matching "category" alone - the literal text the
        // route requires right after it is missing.
        PageRoute.TryMatchPartial(route, "/blog/travel", out _).ShouldBeFalse();
    }

    /// <summary>
    /// FormattedStringValueExtracter keys a captured value by a placeholder's whole inner text, so
    /// "publishTime:yyyy" and "publishTime:MM" are distinct keys - the same field name legitimately
    /// appearing twice with different formats to build a "/2026/07/" path is not a name collision.
    /// </summary>
    [Fact]
    public void Should_Allow_The_Same_Placeholder_Name_With_Different_Formats()
    {
        PageRoute.IsValid("/blog/{publishTime:yyyy}/{publishTime:MM}/{slug}").ShouldBeTrue();
    }

    /// <summary>
    /// What must never repeat is the combined name+format key itself - TryMatchPartial folds every capture
    /// into one dictionary keyed by it, and a second entry with an identical key would throw there instead
    /// of silently overwriting. Case alone does not save it either, since that dictionary is
    /// OrdinalIgnoreCase.
    /// </summary>
    [Fact]
    public void Should_Reject_A_Route_With_A_Duplicate_Placeholder_Key()
    {
        PageRoute.IsValid("/blog/{category}/{category}/{slug}").ShouldBeFalse();
        PageRoute.IsValid("/blog/{Category}/{category}/{slug}").ShouldBeFalse();
        PageRoute.IsValid("/blog/{publishTime:yyyy-MM}/{publishTime:yyyy-MM}/{slug}").ShouldBeFalse();
    }

    /// <summary>
    /// {slug}/{slug?} are recognized whatever case they were typed in - HasSlug, IsSlugOptional and the
    /// slug-must-be-last check in IsValid all key off the same canonicalization, not a literal lowercase
    /// match.
    /// </summary>
    [Fact]
    public void Should_Recognize_Slug_Tokens_Case_Insensitively()
    {
        PageRoute.HasSlug("/blog/{SLUG}").ShouldBeTrue();
        PageRoute.HasSlug("/blog/{Slug?}").ShouldBeTrue();
        PageRoute.IsSlugOptional("/blog/{Slug?}").ShouldBeTrue();
    }

    /// <summary>
    /// A route's literal text is not a placeholder name and gets no case-insensitive treatment - Build
    /// only ever emits one casing, so accepting a differently-cased request as the same match would create
    /// unlimited case-variant duplicate URLs for the same content with no canonical redirect between them.
    /// </summary>
    [Fact]
    public void Should_Match_A_Routes_Literal_Text_Case_Sensitively()
    {
        PageRoute.TryMatchSlug("/blog/post-{slug}", "/blog/post-my-trip", out var slug).ShouldBeTrue();
        slug.ShouldBe("my-trip");

        PageRoute.TryMatchSlug("/blog/post-{slug}", "/blog/POST-my-trip", out _).ShouldBeFalse();

        PageRoute.TryMatchPartial(
            "/blog/post-{category}/{slug}", "/blog/post-travel", out var values).ShouldBeTrue();
        values["category"].ShouldBe("travel");

        PageRoute.TryMatchPartial(
            "/blog/post-{category}/{slug}", "/blog/POST-travel", out _).ShouldBeFalse();
    }
}
