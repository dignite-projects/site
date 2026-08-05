using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.EntityFrameworkCore;
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

    public SiteRouteResolver_Tests()
    {
        _resolver = GetRequiredService<SiteRouteResolver>();
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
            _resolver.ResolveAsync("/news/2026/07/launch", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Content);
        match.Page!.Id.ShouldBe(SiteTestData.NewsPageId);
        match.Content!.Slug.ShouldBe(SiteTestData.NewsSlug);
    }

    /// <summary>
    /// The news page's pattern requires a date segment, so the bare slug is not one of its URLs.
    /// </summary>
    [Fact]
    public async Task Should_Not_Resolve_Dated_Content_Without_Its_Date_Segment()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _resolver.ResolveAsync("/news/launch", SiteTestData.EnglishCulture));

        match.IsMatch.ShouldBeFalse();
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
}
