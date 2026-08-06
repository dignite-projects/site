using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Pages;
using Dignite.Site.Pages;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

public class PageTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly PageTools _pageTools;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;

    public PageTools_Tests()
    {
        _pageTools = GetRequiredService<PageTools>();
        _contentTypeRepository = GetRequiredService<IContentTypeRepository>();
        _contentRepository = GetRequiredService<IContentRepository>();
    }

    /// <summary>
    /// delete_page tells the user it removes every content type and content beneath the page, and asks
    /// them to confirm on that basis. It has to be true.
    /// </summary>
    [Fact]
    public async Task Should_Actually_Remove_Everything_Beneath_A_Deleted_Page()
    {
        await _pageTools.DeletePageAsync("blog");

        (await _contentTypeRepository.FindByNameAsync(SiteTestData.BlogPageId, "post-article"))
            .ShouldBeNull("the content type should be gone with its page");

        (await _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug))
            .ShouldBeNull("the content should be gone with its page");
    }

    /// <summary>And the name must be reusable afterwards, or the section cannot be rebuilt.</summary>
    [Fact]
    public async Task Should_Free_The_Page_Name_After_Deleting_It()
    {
        await _pageTools.DeletePageAsync("blog");

        var recreated = await _pageTools.CreatePageAsync(
            name: "blog", displayName: "Blog", route: "/blog", contentPathPattern: "{slug}");

        recreated.Name.ShouldBe("blog");
    }

    /// <summary>
    /// delete_page's own description now warns that removing the home page leaves the site without one -
    /// the return value has to say so too, since a generic "deleted X" message would hide the one
    /// consequence that matters (总体设计 §5.5: hreflang's x-default needs exactly one home page).
    /// </summary>
    [Fact]
    public async Task Should_Report_When_The_Deleted_Page_Was_The_Home_Page()
    {
        var result = await _pageTools.DeletePageAsync("home");

        result.ShouldContain("home page");
    }

    /// <summary>The mirror image, so the warning cannot be worded so broadly it fires on every delete.</summary>
    [Fact]
    public async Task Should_Not_Mention_The_Home_Page_When_Deleting_An_Ordinary_Page()
    {
        var result = await _pageTools.DeletePageAsync("blog");

        result.ShouldNotContain("home page");
    }

    /// <summary>
    /// The only placeholder syntax the domain actually understands is '{slug}' and '{publishTime:FORMAT}' (see
    /// ContentPathPattern.SupportedPlaceholders) - not the separate '{year}'/'{month}'/'{day}' tokens
    /// create_page's own description named until round 3. Pinned through the tool because the description
    /// is what a model actually reads, and it now has to be true.
    /// </summary>
    [Fact]
    public async Task Should_Create_A_Page_With_A_Date_Based_Content_Path_Pattern()
    {
        var created = await _pageTools.CreatePageAsync(
            name: "events", displayName: "Events", route: "/events",
            contentPathPattern: "{publishTime:yyyy/MM}/{slug}");

        created.ContentPathPattern.ShouldBe("{publishTime:yyyy/MM}/{slug}");
    }

    /// <summary>
    /// `??` only substitutes on null, so an explicit empty string is not "omitted" - it flows through to
    /// Page.SetContentPathPattern, which treats blank the same as null and clears the pattern back to the
    /// default. update_page's own description says this is how to clear it; this pins that the claim is
    /// actually true.
    /// </summary>
    [Fact]
    public async Task Should_Clear_The_Content_Path_Pattern_With_An_Explicit_Empty_String()
    {
        var updated = await _pageTools.UpdatePageAsync(page: "news", contentPathPattern: "");

        updated.ContentPathPattern.ShouldBeNull();
    }

    /// <summary>...and omitting it entirely must keep the news page's dated pattern, not also clear it.</summary>
    [Fact]
    public async Task Should_Keep_The_Content_Path_Pattern_When_Omitted()
    {
        var updated = await _pageTools.UpdatePageAsync(page: "news", displayName: "News (updated)");

        updated.ContentPathPattern.ShouldBe("{publishTime:yyyy/MM}/{slug}");
    }

    /// <summary>
    /// Page.NormalizeRoute prepends the leading slash a model might omit, and PageManager checks
    /// uniqueness against that normalized form - so "blog" (no leading slash) has to collide with the
    /// already-seeded "/blog" exactly as if it had been spelled the same way, not slip through as a
    /// distinct route.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Route_That_Collides_After_Normalization()
    {
        await Should.ThrowAsync<PageRouteAlreadyExistException>(async () =>
            await _pageTools.CreatePageAsync(name: "blog-2", displayName: "Blog Two", route: "blog"));
    }
}
