using System.Threading.Tasks;
using Dignite.Sites.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Sites.Public.Routing;

/// <summary>The HTTP-facing resolve-path contract (总体设计 §7.4), exercised against the seeded blog scenario.</summary>
public class RoutingPublicAppService_Tests : SitesEntityFrameworkCoreTestBase
{
    private readonly IRoutingPublicAppService _routingAppService;

    public RoutingPublicAppService_Tests()
    {
        _routingAppService = GetRequiredService<IRoutingPublicAppService>();
    }

    [Fact]
    public async Task Should_Resolve_A_Page_With_No_Content_Beneath_It_As_Page_Kind()
    {
        var match = await ResolveAsync("/blog");

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.Page);
        match.Page!.Id.ShouldBe(SitesTestData.BlogPageId);
        match.Content.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Resolve_A_Pages_Own_Route_To_Its_Empty_Slug_Content()
    {
        var match = await ResolveAsync("/about");

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.ContentOfPage);
        match.Page!.Id.ShouldBe(SitesTestData.AboutPageId);
        match.Content!.Slug.ShouldBe("");
        match.ContentType!.Id.ShouldBe(SitesTestData.AboutTypeId);
    }

    [Fact]
    public async Task Should_Resolve_A_Content_Beneath_A_Page_By_Slug()
    {
        var match = await ResolveAsync("/blog/" + SitesTestData.TripSlug);

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.Content);
        match.Page!.Id.ShouldBe(SitesTestData.BlogPageId);
        match.Content!.Slug.ShouldBe(SitesTestData.TripSlug);
        match.ContentType!.Id.ShouldBe(SitesTestData.PostArticleTypeId);
        match.Content.FieldValues.ShouldContainKey("title");
    }

    /// <summary>
    /// The public surface never previews a draft - resolving a draft's own URL has to come back exactly
    /// as "nothing here", the same as a path that was never valid at all.
    /// </summary>
    [Fact]
    public async Task Should_Not_Resolve_A_Draft_Content()
    {
        var match = await ResolveAsync("/blog/" + SitesTestData.DraftSlug);

        match.Matched.ShouldBeFalse();
        match.Kind.ShouldBe(RouteMatchKindDto.None);
        match.Page.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Not_Resolve_An_Unknown_Path()
    {
        var match = await ResolveAsync("/this-path-does-not-exist");

        match.Matched.ShouldBeFalse();
        match.Kind.ShouldBe(RouteMatchKindDto.None);
    }

    private Task<RouteMatchDto> ResolveAsync(string path)
    {
        return _routingAppService.ResolveAsync(
            new ResolvePathInput { Path = path, CultureName = SitesTestData.EnglishCulture });
    }
}
