using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Pages;
using Dignite.Site.Mcp.Routing;
using Dignite.Site.Public.Routing;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// resolve_path carries no [Authorize] of its own (总体设计 §6.2.5) - the guard is that it only ever
/// resolves against published content on a routable page. These tests exist to pin that the guard
/// actually holds through the MCP wrapper specifically: a draft, and content beneath a page an operator
/// has switched off, must come back looking exactly like a path that was never valid at all, not as a
/// distinguishable "it's there, just not for you" answer.
/// <para>
/// The draft case is already covered at the AppService layer by
/// <c>RoutingPublicAppService_Tests</c> and at the domain layer by <c>SiteRouteResolver_Tests</c>; it is
/// re-asserted here because this tool is the one place in the whole stack with no permission attribute of
/// its own, so a regression here is the one that would actually reach an under-privileged caller. The
/// inactive-page case had no coverage anywhere before this file - the seeded scenario has no inactive
/// page, so the tests switch one off themselves using the very tool this review covers.
/// </para>
/// </summary>
public class RoutingTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly RoutingTools _routingTools;
    private readonly PageTools _pageTools;

    public RoutingTools_Tests()
    {
        _routingTools = GetRequiredService<RoutingTools>();
        _pageTools = GetRequiredService<PageTools>();
    }

    [Fact]
    public async Task Should_Resolve_A_Published_Content_Beneath_A_Page()
    {
        var match = await _routingTools.ResolvePathAsync("/blog/" + SiteTestData.TripSlug, SiteTestData.EnglishCulture);

        match.Matched.ShouldBeTrue();
        match.Kind.ShouldBe(RouteMatchKindDto.Content);
        match.Page!.Id.ShouldBe(SiteTestData.BlogPageId);
        match.Content!.Slug.ShouldBe(SiteTestData.TripSlug);
    }

    [Fact]
    public async Task Should_Not_Resolve_A_Draft_Content()
    {
        var match = await _routingTools.ResolvePathAsync("/blog/" + SiteTestData.DraftSlug, SiteTestData.EnglishCulture);

        match.Matched.ShouldBeFalse();
        match.Kind.ShouldBe(RouteMatchKindDto.None);
        match.Page.ShouldBeNull();
        match.Content.ShouldBeNull();
    }

    /// <summary>
    /// "An inactive page is not routable" (<c>Page.IsActive</c>'s own doc) - so its own route must stop
    /// resolving once it is switched off, the same as if it had never existed.
    /// </summary>
    [Fact]
    public async Task Should_Not_Resolve_A_Pages_Own_Route_Once_Deactivated()
    {
        await _pageTools.UpdatePageAsync(page: "about", isActive: false);

        var match = await _routingTools.ResolvePathAsync("/about", SiteTestData.EnglishCulture);

        match.Matched.ShouldBeFalse();
        match.Kind.ShouldBe(RouteMatchKindDto.None);
    }

    /// <summary>
    /// ...and so must a content beneath it, or deactivating the page would only hide the page's own URL
    /// while every content under it stayed publicly reachable - the opposite of "not routable".
    /// </summary>
    [Fact]
    public async Task Should_Not_Resolve_Content_Beneath_A_Deactivated_Page()
    {
        await _pageTools.UpdatePageAsync(page: "blog", isActive: false);

        var match = await _routingTools.ResolvePathAsync("/blog/" + SiteTestData.TripSlug, SiteTestData.EnglishCulture);

        match.Matched.ShouldBeFalse();
        match.Kind.ShouldBe(RouteMatchKindDto.None);
    }

    /// <summary>
    /// A culture no CultureInfo recognizes is not a lookup miss to investigate - it cannot name a row, so
    /// it resolves to nothing rather than throwing into a tool that carries no [Authorize] of its own. An
    /// ArgumentException surfacing as an opaque internal error would be the worse failure mode here, not
    /// just an inconvenient one (总体设计 §2.4).
    /// </summary>
    [Fact]
    public async Task Should_Return_No_Match_Rather_Than_Throw_For_An_Unrecognized_Culture()
    {
        var match = await _routingTools.ResolvePathAsync("/about", "not-a-culture");

        match.Matched.ShouldBeFalse();
    }
}
