using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Shouldly;
using Xunit;

namespace Dignite.Site.Routing;

/// <summary>
/// 总体设计 §9's P0 exit criterion: a blog resolving as a single page, a list and a detail view, off one
/// routing table with no page-kind flag anywhere in it.
/// </summary>
public class SiteRouteResolver_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteRouteResolver _resolver;
    private readonly PageManager _pageManager;
    private readonly IFieldRepository _fieldRepository;
    private readonly ContentTypeManager _contentTypeManager;
    private readonly ContentManager _contentManager;

    public SiteRouteResolver_Tests()
    {
        _resolver = GetRequiredService<SiteRouteResolver>();
        _pageManager = GetRequiredService<PageManager>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _contentManager = GetRequiredService<ContentManager>();
    }

    /// <summary>
    /// "/about" is a page carrying one empty-slug content, so the URL means that content - a single page.
    /// </summary>
    [Fact]
    public async Task Should_Resolve_Single_Page()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/about", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.ContentOfPage);
        match.Page!.Id.ShouldBe(SiteTestData.AboutPageId);
        match.Content!.Slug.ShouldBe("");
        match.ContentType!.Id.ShouldBe(SiteTestData.AboutTypeId);
    }

    [Fact]
    public async Task Should_Resolve_Home_Page_At_Root()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.ContentOfPage);
        match.Page!.Id.ShouldBe(SiteTestData.HomePageId);
        match.Page.IsHomePage.ShouldBeTrue();
    }

    /// <summary>
    /// "/blog" is a page with no empty-slug content beneath it, so it resolves to the page alone and a
    /// front end renders a list. Nothing in the model marked it as a "list page".
    /// </summary>
    [Fact]
    public async Task Should_Resolve_List_Page()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/blog", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Page);
        match.Page!.Id.ShouldBe(SiteTestData.BlogPageId);
        match.Content.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Resolve_Detail_Page()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/blog/my-trip", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Content);
        match.Page!.Id.ShouldBe(SiteTestData.BlogPageId);
        match.Content!.Slug.ShouldBe(SiteTestData.TripSlug);
        match.ContentType!.Id.ShouldBe(SiteTestData.PostArticleTypeId);
    }

    /// <summary>
    /// The same page serving two content shapes - which is what lets a front end pick a gallery template
    /// for one post and an article template for another, with no second page involved.
    /// </summary>
    [Fact]
    public async Task Should_Resolve_Detail_Of_Another_Content_Type_Under_Same_Page()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/blog/summer-photos", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Content);
        match.Page!.Id.ShouldBe(SiteTestData.BlogPageId);
        match.ContentType!.Id.ShouldBe(SiteTestData.PostGalleryTypeId);
    }

    [Fact]
    public async Task Should_Resolve_Detail_Under_Dated_Path_Pattern()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/news/2026-07/launch", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Content);
        match.Page!.Id.ShouldBe(SiteTestData.NewsPageId);
        match.Content!.Slug.ShouldBe(SiteTestData.NewsSlug);
    }

    /// <summary>
    /// "/news/launch" is one segment short of the news page's full "date + slug" shape, so it cannot
    /// resolve to content - but it fully satisfies the page's route up to (not including) slug, so it is a
    /// partial match instead of a miss. FormattedStringValueExtracter never validates a captured value
    /// against its own :FORMAT (see PageRoute_Tests.Should_Fill_A_Placeholder_Regardless_Of_Whether_The_
    /// Value_Matches_Its_Format) - "launch" fills publishTime:yyyy-MM exactly as readily as a real month
    /// would, garbage and all.
    /// </summary>
    [Fact]
    public async Task Should_Resolve_A_Dated_Path_Without_A_Slug_As_A_Partial_Match()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/news/launch", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Page);
        match.Page!.Id.ShouldBe(SiteTestData.NewsPageId);
        match.Content.ShouldBeNull();
        match.FilterValues["publishTime:yyyy-MM"].ShouldBe("launch");
    }

    /// <summary>
    /// The positive case alongside the one above: a value that actually looks like the format asks for
    /// resolves exactly the same way - PageRoute never distinguishes "looks valid" from "looks like
    /// garbage", so neither does this.
    /// </summary>
    [Fact]
    public async Task Should_Resolve_A_Dated_Path_Without_A_Slug_As_A_Partial_Match_With_A_Real_Month()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/news/2026-07", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Page);
        match.Page!.Id.ShouldBe(SiteTestData.NewsPageId);
        match.FilterValues["publishTime:yyyy-MM"].ShouldBe("2026-07");
    }

    [Fact]
    public async Task Should_Not_Resolve_Unpublished_Content_Publicly()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/blog/draft-post", SiteTestData.EnglishCulture));

        match.IsMatch.ShouldBeFalse();
    }

    /// <summary>
    /// ...but a preview URL can ask for it. A caller doing so has to force noindex on the response.
    /// </summary>
    [Fact]
    public async Task Should_Resolve_Unpublished_Content_When_Previewing()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/blog/draft-post", SiteTestData.EnglishCulture, includeUnpublished: true));

        match.Kind.ShouldBe(RouteMatchKind.Content);
        match.Content!.Slug.ShouldBe(SiteTestData.DraftSlug);
    }

    /// <summary>
    /// One row is one language, so a content that exists only in English must not answer a Chinese
    /// request by falling back.
    /// </summary>
    [Fact]
    public async Task Should_Not_Fall_Back_Across_Languages()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/blog/my-trip", SiteTestData.ChineseCulture));

        match.IsMatch.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Resolve_The_Language_That_Exists()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/about", SiteTestData.ChineseCulture));

        match.Kind.ShouldBe(RouteMatchKind.ContentOfPage);
        match.Content!.CultureName.ShouldBe(SiteTestData.ChineseCulture);
        match.Content.GetField<string>("title").ShouldBe("关于我们");
    }

    /// <summary>
    /// A culture that .NET does not recognize is not a lookup miss to investigate - it cannot name a row,
    /// so it resolves to nothing rather than throwing into the request pipeline.
    /// </summary>
    [Fact]
    public async Task Should_Not_Match_On_Unrecognized_Culture()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/about", "not-a-culture"));

        match.IsMatch.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Not_Match_Unknown_Path()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/no/such/path", SiteTestData.EnglishCulture));

        match.IsMatch.ShouldBeFalse();
        match.Page.ShouldBeNull();
    }

    /// <summary>
    /// A fresh templated page's route (/prefix-priority/{publishTime:yyyy-MM}/{slug}) already derives its
    /// own address to "/prefix-priority" - the same address a literal page at "/prefix-priority" would
    /// have. Creating both is not a creation-time conflict (see PageManager_Tests), and a bare
    /// "/prefix-priority" request now resolves to the more specific literal page deterministically, not to
    /// whichever of the two a Route string sort happens to favor. The templated page's own deep URLs are
    /// unaffected either way - they never needed the bare address to serve their content. Deliberately its
    /// own route family, not "/news" or "/blog" - creating a literal page here must not leak into any
    /// other test in this class that resolves against those.
    /// </summary>
    [Fact]
    public async Task Should_Prefer_A_Literal_Route_Over_A_Templated_Route_Sharing_Its_Own_Address()
    {
        var templated = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "prefix-priority-deep", "Prefix priority deep",
            "/prefix-priority/{publishTime:yyyy-MM}/{slug}"));

        var bareMatchBeforeLiteral = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/prefix-priority", SiteTestData.EnglishCulture));

        bareMatchBeforeLiteral.Kind.ShouldBe(RouteMatchKind.Page);
        bareMatchBeforeLiteral.Page!.Id.ShouldBe(templated.Id);

        await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("prefix-priority-index", "Prefix priority index", "/prefix-priority"));

        var bareMatchAfterLiteral = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/prefix-priority", SiteTestData.EnglishCulture));

        bareMatchAfterLiteral.Kind.ShouldBe(RouteMatchKind.Page);
        bareMatchAfterLiteral.Page!.Name.ShouldBe("prefix-priority-index");
        bareMatchAfterLiteral.Page.Id.ShouldNotBe(templated.Id);
    }

    /// <summary>
    /// An intermediate literal segment ("/shop/electronics") is not automatically the deep templated
    /// page's own territory just because that page's derived address happens to land there too - but
    /// since the fix for the scenario that overturned the first version of this design, it only wins when
    /// nothing at this length structurally accounts for the rest of the path. Here the deep page's own
    /// TryMatchSlug does fill every placeholder including slug, shaped exactly like its own detail URL -
    /// but no content was ever published under it, so that structural attempt is itself a miss, and the
    /// literal page is what is left once both candidates at this length have had their turn (总体设计 §3.4,
    /// SiteRouteResolver.ResolveCoreAsync's two-pass remarks). A request where real content backs that
    /// slug instead is Should_Resolve_Content_Under_A_Deeper_Template_Even_When_An_Unrelated_Literal_Page_
    /// Shares_Its_Own_Address's job, not this one's.
    /// </summary>
    [Fact]
    public async Task Should_Let_An_Unrelated_Literal_Page_Win_When_The_Deeper_Templates_Structural_Match_Finds_No_Content()
    {
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "shop-electronics-deep", "Electronics deep",
            "/shop/electronics/{publishTime:yyyy-MM}/{slug}"));
        var literal = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("shop-electronics-index", "Electronics index", "/shop/electronics"));

        var detailShapedMatch = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/shop/electronics/2026-08/some-item", SiteTestData.EnglishCulture));

        detailShapedMatch.Kind.ShouldBe(RouteMatchKind.Page);
        detailShapedMatch.Page!.Id.ShouldBe(literal.Id);
    }

    /// <summary>
    /// Superseded by Should_Prefer_An_Unrelated_Literal_Page_Over_The_Deeper_Templates_Partial_Match_When_
    /// Both_Share_An_Address below: a deep template's truncated (short-of-slug) reading of an address is no
    /// longer offered at all once a second page shares that same address - literal or not, and regardless
    /// of whether that second page can itself answer the request (总体设计 §3.4). A truncated reading is
    /// only ever tried when the deep template is the address's sole candidate - see
    /// Should_Resolve_A_Dated_Path_Without_A_Slug_As_A_Partial_Match(_With_A_Real_Month) above for that case,
    /// and Should_Prefer_A_Dedicated_Non_Slug_Template_Over_The_Deeper_Templates_Truncated_Match below for
    /// the sibling that is meant to win instead when one exists.
    /// </summary>
    [Fact]
    public async Task Should_Prefer_An_Unrelated_Literal_Page_Over_The_Deeper_Templates_Partial_Match_When_Both_Share_An_Address()
    {
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "shop-furniture-deep", "Furniture deep",
            "/shop/furniture/{publishTime:yyyy-MM}/{slug}"));
        var literal = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("shop-furniture-index", "Furniture index", "/shop/furniture"));

        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/shop/furniture/2026-08", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Page);
        match.Page!.Id.ShouldBe(literal.Id);
        match.FilterValues.ShouldBeEmpty();
    }

    /// <summary>
    /// The capability the whole three-tier redesign exists to unlock: a page dedicated to the month-index
    /// view (no {slug} at all) sharing an address with a deeper detail template wins outright via
    /// PageRoute.TryMatchExact - tier 1, not a truncated guess - so the deep template's own truncated
    /// reading is never even attempted, and the FilterValues an admin-built month-index page actually needs
    /// come from the dedicated page, not a borrowed reading of the detail template.
    /// </summary>
    [Fact]
    public async Task Should_Prefer_A_Dedicated_Non_Slug_Template_Over_The_Deeper_Templates_Truncated_Match()
    {
        var dedicated = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "updates-month-index", "Updates month index", "/updates/{publishTime:yyyy-MM}"));
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "updates-deep", "Updates deep", "/updates/{publishTime:yyyy-MM}/{slug}"));

        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/updates/2026-07", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Page);
        match.Page!.Id.ShouldBe(dedicated.Id);
        match.FilterValues["publishTime:yyyy-MM"].ShouldBe("2026-07");
    }

    /// <summary>
    /// The precise shape of the motivating example: a page that declares slug support (so it is not merely
    /// a bare, mechanism-less literal - see the test above for that case) shares an address with a deeper
    /// template, but no content exists at either candidate's reading of "2026-07" - not as this page's own
    /// slug, and not (since a second candidate exists here) via the deep template's suppressed truncated
    /// reading either. Both candidates give their final answer within tier 1 - one a definitive miss, the
    /// other excluded because its own placeholders leave "2026-07" unaccounted for - so tier 2's "page
    /// itself" fallback is never reached by either, and this whole prefix length fails outright: a 404, not
    /// a silent, misleading render of the wrong page.
    /// </summary>
    [Fact]
    public async Task Should_Not_Resolve_When_A_Slug_Declaring_Siblings_Own_Lookup_Misses_And_The_Deeper_Template_Cannot_Naturally_Fit()
    {
        await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("archive-index", "Archive index", "/archive/{slug?}"));
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "archive-deep", "Archive deep", "/archive/{publishTime:yyyy-MM}/{slug}"));

        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/archive/2026-07", SiteTestData.EnglishCulture));

        match.IsMatch.ShouldBeFalse();
    }

    /// <summary>
    /// The regex constraint (Part 1) rejecting a wrongly-shaped segment is a separate mechanism from the
    /// tier-3 sole-candidacy gate (Part 2) - this pins it independently, on a route with no sibling at all,
    /// so the rejection can only be the regex, not suppression.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Partial_Match_Whose_Captured_Value_Fails_Its_Regex_Constraint()
    {
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "bulletins-deep", "Bulletins deep",
            @"/bulletins/{publishTime:yyyy-MM:^\d{4}-(0[1-9]|1[0-2])$}/{slug}"));

        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/bulletins/not-a-date", SiteTestData.EnglishCulture));

        match.IsMatch.ShouldBeFalse();
    }

    /// <summary>
    /// The actual shape of bug report GitHub issue #46: a literal page built after a templated page
    /// already existed, deriving the same own address, used to shadow every deeper URL under that address
    /// unconditionally - even ones with real, published content behind them, not just the never-populated
    /// pages the two tests above exercise. It no longer does: the templated page's own TryMatchSlug fills
    /// every placeholder and finds the real content first, in the same pass, before the literal page's
    /// fallback ever runs. A fresh route family (not "/news" or "/blog") - this test class shares one
    /// database across every test in the assembly, so creating a colliding literal page against the seeded
    /// News/Blog pages here would leak into whichever other test happens to run after it.
    /// </summary>
    [Fact]
    public async Task Should_Resolve_Content_Under_A_Deeper_Template_Even_When_An_Unrelated_Literal_Page_Shares_Its_Own_Address()
    {
        var deep = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "press-releases-deep", "Press releases deep",
            "/press-releases/{publishTime:yyyy-MM}/{slug}"));
        var literal = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("press-releases-index", "Press releases index", "/press-releases"));

        var titleField = await WithUnitOfWorkAsync(() => _fieldRepository.GetAsync(SiteTestData.TitleFieldId));

        var contentTypeId = await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeManager.CreateAsync(
                deep.Id, "press-release-item", "Press release item",
                fields: new[] { new ContentTypeField(titleField.Id, order: 0) });
            return contentType.Id;
        });

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, "launch-day", SiteTestData.PublishTime,
            ContentStatus.Published, new Dictionary<string, object?> { ["title"] = "Launch day" }));

        var detailMatch = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/press-releases/2026-07/launch-day", SiteTestData.EnglishCulture));

        detailMatch.Kind.ShouldBe(RouteMatchKind.Content);
        detailMatch.Page!.Id.ShouldBe(deep.Id);
        detailMatch.Content!.Slug.ShouldBe("launch-day");

        // The bare address is still the literal page's own, unambiguously - fixing the detail URL above
        // must not touch it.
        var bareMatch = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/press-releases", SiteTestData.EnglishCulture));

        bareMatch.Kind.ShouldBe(RouteMatchKind.Page);
        bareMatch.Page!.Id.ShouldBe(literal.Id);
    }

    /// <summary>
    /// The bug this review round caught: the old design committed to the first candidate at a prefix
    /// length and gave up the moment its content lookup missed, even with a second, equally-eligible
    /// candidate at that same length still untried. "/multi-x/{slug?}" sorts ahead of "/multi-x/{slug}"
    /// (Ordinal: '?' &lt; '}') but carries no content of its own here, so a request for content that only
    /// exists under the second page must fall through to it rather than dead-end in a 404.
    /// </summary>
    [Fact]
    public async Task Should_Retry_The_Next_Candidate_At_The_Same_Length_When_The_First_Ones_Content_Lookup_Misses()
    {
        var (emptyPage, contentPage) = await WithUnitOfWorkAsync(async () =>
        {
            var optionalSlugPage = await _pageManager.CreateAsync(
                "multi-x-optional", "Multi x optional", "/multi-x/{slug?}");
            var mandatorySlugPage = await _pageManager.CreateAsync(
                "multi-x-mandatory", "Multi x mandatory", "/multi-x/{slug}");
            return (optionalSlugPage, mandatorySlugPage);
        });

        var titleField = await WithUnitOfWorkAsync(() => _fieldRepository.GetAsync(SiteTestData.TitleFieldId));

        var contentTypeId = await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeManager.CreateAsync(
                contentPage.Id, "multi-x-item", "Multi x item",
                fields: new[] { new ContentTypeField(titleField.Id, order: 0) });
            return contentType.Id;
        });

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, "real-item", SiteTestData.PublishTime,
            ContentStatus.Published, new Dictionary<string, object?> { ["title"] = "Real item" }));

        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/multi-x/real-item", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Content);
        match.Page!.Id.ShouldBe(contentPage.Id);
        match.Page.Id.ShouldNotBe(emptyPage.Id);
        match.Content!.Slug.ShouldBe("real-item");
    }

    /// <summary>
    /// A doubled slash must not silently collapse to a clean, shorter prefix on the way through - it is
    /// rejected outright rather than resolving identically to the equivalent clean path.
    /// </summary>
    [Fact]
    public async Task Should_Not_Resolve_A_Path_With_A_Doubled_Slash()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/blog//my-trip", SiteTestData.EnglishCulture));

        match.IsMatch.ShouldBeFalse();
    }
}
