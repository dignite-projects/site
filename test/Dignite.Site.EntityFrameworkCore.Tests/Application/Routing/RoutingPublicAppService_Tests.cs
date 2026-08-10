using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Routing;

/// <summary>The HTTP-facing resolve-path contract (总体设计 §7.4), exercised against the seeded blog scenario.</summary>
public class RoutingPublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IRoutingPublicAppService _routingAppService;

    public RoutingPublicAppService_Tests()
    {
        _routingAppService = GetRequiredService<IRoutingPublicAppService>();

        // ResolveAsync now strips a culture prefix itself, which needs a SiteUrlContext, which needs a
        // configured base URL.
        GetRequiredService<TestSettingValueProvider>().Set(SiteSettings.PrimaryDomain, "https://acme.example");
    }

    [Fact]
    public async Task Should_Resolve_A_Page_With_No_Content_Beneath_It_As_Page_Kind()
    {
        var match = await ResolveAsync("/blog");

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.Page);
        match.Page!.Id.ShouldBe(SiteTestData.BlogPageId);
        match.Content.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Resolve_A_Pages_Own_Route_To_Its_Empty_Slug_Content()
    {
        var match = await ResolveAsync("/about");

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.ContentOfPage);
        match.Page!.Id.ShouldBe(SiteTestData.AboutPageId);
        match.Content!.Slug.ShouldBe("");
        match.ContentType!.Id.ShouldBe(SiteTestData.AboutTypeId);
    }

    [Fact]
    public async Task Should_Resolve_A_Content_Beneath_A_Page_By_Slug()
    {
        var match = await ResolveAsync("/blog/" + SiteTestData.TripSlug);

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.Content);
        match.Page!.Id.ShouldBe(SiteTestData.BlogPageId);
        match.Content!.Slug.ShouldBe(SiteTestData.TripSlug);
        match.ContentType!.Id.ShouldBe(SiteTestData.PostArticleTypeId);
        match.Content.FieldValues.ShouldContainKey("title");
    }

    /// <summary>
    /// The public surface never previews a draft - resolving a draft's own URL has to come back exactly
    /// as "nothing here", the same as a path that was never valid at all.
    /// </summary>
    [Fact]
    public async Task Should_Not_Resolve_A_Draft_Content()
    {
        var match = await ResolveAsync("/blog/" + SiteTestData.DraftSlug);

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

    /// <summary>A partial match's FilterValues has to survive the domain-to-DTO mapping, not just Kind/Page.</summary>
    [Fact]
    public async Task Should_Populate_FilterValues_For_A_Partial_Match()
    {
        var match = await ResolveAsync("/news/2026-07");

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.Page);
        match.Page!.Id.ShouldBe(SiteTestData.NewsPageId);
        match.Content.ShouldBeNull();
        match.FilterValues["publishTime:yyyy-MM"].ShouldBe("2026-07");
    }

    [Fact]
    public async Task Should_Have_Empty_FilterValues_For_A_Bare_Address()
    {
        var match = await ResolveAsync("/blog");

        match.FilterValues.ShouldBeEmpty();
    }

    /// <summary>
    /// The domain's FilterValues dictionary is OrdinalIgnoreCase (PageRoute.TryMatchPartial's own
    /// contract) - copying it into the DTO with `new Dictionary&lt;&gt;(source)` alone would silently drop
    /// that comparer and fall back to the default ordinal one, so a lookup by a differently-cased key
    /// would stop finding the value even though the key is "the same" by every rule this route uses.
    /// </summary>
    [Fact]
    public async Task Should_Look_Up_FilterValues_Case_Insensitively()
    {
        var match = await ResolveAsync("/news/2026-07");

        match.FilterValues["PUBLISHTIME:YYYY-MM"].ShouldBe("2026-07");
    }

    private Task<RouteMatchDto> ResolveAsync(string path)
    {
        return _routingAppService.ResolveAsync(new ResolvePathInput { Path = path });
    }
}
