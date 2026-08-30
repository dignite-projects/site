using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Pages;
using Dignite.Site.Pages;
using Shouldly;
using Volo.Abp.Validation;
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
            name: "blog", displayName: "Blog", route: "/blog/{slug}", template: "Default");

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
    /// A route's placeholders are not limited to '{slug}' and '{publishTime:FORMAT}' - any field the
    /// content has can appear (see PageRoute's remarks) - but a FORMAT is restricted to characters that
    /// can never collide with the slashes between path segments, so 'yyyy-MM' rather than 'yyyy/MM'.
    /// Pinned through the tool because the description is what a model actually reads, and it now has to
    /// be true.
    /// </summary>
    [Fact]
    public async Task Should_Create_A_Page_With_A_Date_Based_Route()
    {
        var created = await _pageTools.CreatePageAsync(
            name: "events", displayName: "Events", route: "/events/{publishTime:yyyy-MM}/{slug}",
            template: "Default");

        created.Route.ShouldBe("/events/{publishTime:yyyy-MM}/{slug}");
    }

    /// <summary>...and omitting "route" entirely must keep the news page's route unchanged.</summary>
    [Fact]
    public async Task Should_Keep_The_Route_When_Omitted()
    {
        var updated = await _pageTools.UpdatePageAsync(page: "news", displayName: "News (updated)");

        updated.Route.ShouldBe("/news/{publishTime:yyyy-MM}/{slug}");
    }

    /// <summary>
    /// Turning a page that has content beneath it back into one that does not is just an ordinary route
    /// update - no separate flag or special value, since {slug}'s presence is the only signal there ever
    /// was for whether a page has content.
    /// </summary>
    [Fact]
    public async Task Should_Turn_A_Page_With_Content_Into_A_Single_Page_By_Dropping_The_Slug()
    {
        var updated = await _pageTools.UpdatePageAsync(page: "blog", route: "/blog");

        updated.Route.ShouldBe("/blog");
    }

    /// <summary>
    /// Page.NormalizeRoute prepends the leading slash a model might omit, and PageManager checks
    /// uniqueness against that normalized form - so "about" (no leading slash) has to collide with the
    /// already-seeded "/about" exactly as if it had been spelled the same way, not slip through as a
    /// distinct route. Deliberately picked over "blog": the seeded blog page's route is the template
    /// "/blog/{slug?}", a different literal string from "/blog" - RouteExistsAsync only rejects a literal
    /// duplicate (see PageManager_Tests for why a literal route and a template route sharing an address
    /// are allowed to coexist), so that pairing would not collide here.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Route_That_Collides_After_Normalization()
    {
        await Should.ThrowAsync<PageRouteAlreadyExistException>(async () =>
            await _pageTools.CreatePageAsync(
                name: "about-2", displayName: "About Two", route: "about", template: "Default"));
    }

    /// <summary>create_page's "parent" takes a machine name like every other reference in this surface, not a Guid.</summary>
    [Fact]
    public async Task Should_Create_A_Page_Under_A_Parent_By_Name()
    {
        var created = await _pageTools.CreatePageAsync(
            name: "parent-test-child", displayName: "Child", route: "/parent-test-child",
            template: "Default", parent: "blog");

        created.ParentId.ShouldBe(SiteTestData.BlogPageId);
    }

    /// <summary>update_page's "parent" resolves the same way create_page's does.</summary>
    [Fact]
    public async Task Should_Reparent_A_Page_By_Name()
    {
        var updated = await _pageTools.UpdatePageAsync(page: "news", parent: "blog");

        updated.ParentId.ShouldBe(SiteTestData.BlogPageId);
    }

    /// <summary>
    /// Mirrors Should_Keep_The_Content_Path_Pattern_When_Omitted: omitting "parent" must not disturb a
    /// parent set by an earlier call, the same null-keeps convention as every other optional field here.
    /// </summary>
    [Fact]
    public async Task Should_Keep_The_Parent_When_Omitted()
    {
        await _pageTools.UpdatePageAsync(page: "news", parent: "blog");

        var updated = await _pageTools.UpdatePageAsync(page: "news", displayName: "News (updated)");

        updated.ParentId.ShouldBe(SiteTestData.BlogPageId);
    }

    /// <summary>
    /// Mirrors Should_Clear_The_Content_Path_Pattern_With_An_Explicit_Empty_String: "parent" cannot reuse
    /// null for "clear it", since null already means "leave it alone" - an explicit empty string is the
    /// only way to ask for a top-level page back.
    /// </summary>
    [Fact]
    public async Task Should_Clear_The_Parent_With_An_Explicit_Empty_String()
    {
        await _pageTools.UpdatePageAsync(page: "news", parent: "blog");

        var updated = await _pageTools.UpdatePageAsync(page: "news", parent: "");

        updated.ParentId.ShouldBeNull();
    }

    /// <summary>
    /// create_page went without a 'template' parameter entirely until this test was written -
    /// CreatePageDto carried the field from the start, but nothing on the MCP surface could reach it. See
    /// Dignite.Site.Mcp.McpToolDtoContract_Tests for the structural check that now catches a gap like that
    /// without needing a behavioral test like this one for every field.
    /// </summary>
    [Fact]
    public async Task Should_Create_A_Page_With_A_Rendering_Template()
    {
        var created = await _pageTools.CreatePageAsync(
            name: "mcp-template-create", displayName: "Template Create", route: "/mcp-template-create",
            template: "pages/list");

        created.Template.ShouldBe("pages/list");
    }

    [Fact]
    public async Task Should_Update_The_Rendering_Template()
    {
        var updated = await _pageTools.UpdatePageAsync(page: "blog", template: "pages/list");

        updated.Template.ShouldBe("pages/list");
    }

    /// <summary>Mirrors Should_Keep_The_Parent_When_Omitted: the same null-keeps convention applies here.</summary>
    [Fact]
    public async Task Should_Keep_The_Rendering_Template_When_Omitted()
    {
        await _pageTools.UpdatePageAsync(page: "blog", template: "pages/list");

        var updated = await _pageTools.UpdatePageAsync(page: "blog", displayName: "Blog (updated)");

        updated.Template.ShouldBe("pages/list");
    }

    /// <summary>
    /// Template is required (issue #53) - unlike 'parent', an explicit empty string cannot clear it back
    /// to nothing, it can only ever replace it with a different, still-valid view name.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Clearing_The_Rendering_Template_With_An_Explicit_Empty_String()
    {
        await Should.ThrowAsync<AbpValidationException>(async () =>
            await _pageTools.UpdatePageAsync(page: "blog", template: ""));
    }

    /// <summary>
    /// The same PageHasChildrenException PageManager_Tests pins at the domain layer has to actually reach
    /// an MCP caller, not get swallowed or rewrapped on the way through PageAdminAppService.
    /// </summary>
    [Fact]
    public async Task Should_Fail_To_Delete_A_Page_That_Has_Children()
    {
        await _pageTools.CreatePageAsync(
            name: "mcp-delete-guard-parent", displayName: "Parent", route: "/mcp-delete-guard-parent",
            template: "Default");
        await _pageTools.CreatePageAsync(
            name: "mcp-delete-guard-child", displayName: "Child", route: "/mcp-delete-guard-child",
            template: "Default", parent: "mcp-delete-guard-parent");

        await Should.ThrowAsync<PageHasChildrenException>(async () =>
            await _pageTools.DeletePageAsync("mcp-delete-guard-parent"));
    }
}
