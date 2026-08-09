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
    /// The scenario that overturned the first version of this design: an intermediate literal segment
    /// ("/shop/electronics") is not automatically the deep templated page's own territory just because
    /// that page's derived address happens to land there too - a separately-created, unrelated literal
    /// page at that exact address wins the moment it exists, at every length below it in the same request,
    /// including a path that would otherwise have been the deep page's own detail URL. The engine commits
    /// to the longest matching address and never backtracks to a shorter, different page just because what
    /// follows does not fit - see SiteRouteResolver.ResolveAsync's remarks.
    /// </summary>
    [Fact]
    public async Task Should_Let_An_Unrelated_Literal_Page_Shadow_A_Deeper_Templates_Own_Prefix()
    {
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "shop-electronics-deep", "Electronics deep",
            "/shop/electronics/{publishTime:yyyy-MM}/{slug}"));
        var literal = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("shop-electronics-index", "Electronics index", "/shop/electronics"));

        var partialShapedMatch = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/shop/electronics/2026-08", SiteTestData.EnglishCulture));

        partialShapedMatch.Kind.ShouldBe(RouteMatchKind.Page);
        partialShapedMatch.Page!.Id.ShouldBe(literal.Id);
        partialShapedMatch.FilterValues.ShouldBeEmpty();

        // Even a path shaped like the deep page's own detail URL never reaches it - the literal page at
        // "/shop/electronics" wins first and is final, with nothing left to backtrack to.
        var detailShapedMatch = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/shop/electronics/2026-08/some-item", SiteTestData.EnglishCulture));

        detailShapedMatch.Kind.ShouldBe(RouteMatchKind.Page);
        detailShapedMatch.Page!.Id.ShouldBe(literal.Id);
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
