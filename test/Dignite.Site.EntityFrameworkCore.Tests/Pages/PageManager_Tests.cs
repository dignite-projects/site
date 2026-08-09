using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Pages;

public class PageManager_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly PageManager _pageManager;
    private readonly IPageRepository _pageRepository;

    public PageManager_Tests()
    {
        _pageManager = GetRequiredService<PageManager>();
        _pageRepository = GetRequiredService<IPageRepository>();
    }

    /// <summary>
    /// The most direct cycle: a page cannot be its own parent. CheckParentAsync's fast path catches this
    /// before it ever walks an ancestor chain.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Page_As_Its_Own_Parent()
    {
        var page = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("self-parent-test", "Self parent test", "/self-parent-test"));

        await Should.ThrowAsync<PageParentCycleException>(() => WithUnitOfWorkAsync(async () =>
        {
            var reloaded = await _pageRepository.GetAsync(page.Id);
            await _pageManager.UpdateAsync(
                reloaded, reloaded.Name, reloaded.DisplayName, reloaded.Route, parentId: reloaded.Id);
        }));
    }

    /// <summary>
    /// A longer cycle than the direct case above: A is B's parent, and B is C's parent, so making C the
    /// parent of A would loop A -&gt; C -&gt; B -&gt; A. CheckParentAsync has to walk the whole chain, not
    /// just check the immediate parent, to catch this.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Multi_Level_Parent_Cycle()
    {
        var a = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync("cycle-a", "A", "/cycle-a"));
        var b = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("cycle-b", "B", "/cycle-b", parentId: a.Id));
        var c = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("cycle-c", "C", "/cycle-c", parentId: b.Id));

        await Should.ThrowAsync<PageParentCycleException>(() => WithUnitOfWorkAsync(async () =>
        {
            var reloadedA = await _pageRepository.GetAsync(a.Id);
            await _pageManager.UpdateAsync(
                reloadedA, reloadedA.Name, reloadedA.DisplayName, reloadedA.Route, parentId: c.Id);
        }));
    }

    /// <summary>
    /// A parent id that does not exist surfaces as the framework's own EntityNotFoundException, not a
    /// bespoke error - CheckParentAsync confirms existence by reading the row, not by a separate check.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Nonexistent_Parent()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync(
                "orphan-attempt", "Orphan attempt", "/orphan-attempt", parentId: Guid.NewGuid())));
    }

    /// <summary>
    /// Deleting a page cascades to its own content types and contents, but not to child pages - each one
    /// can carry its own content types and contents, so the blast radius of one delete has to stay
    /// bounded rather than taking a whole subtree down with it.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Deleting_A_Page_That_Has_Children()
    {
        var parent = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("delete-guard-parent", "Parent", "/delete-guard-parent"));
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "delete-guard-child", "Child", "/delete-guard-child", parentId: parent.Id));

        await Should.ThrowAsync<PageHasChildrenException>(() => WithUnitOfWorkAsync(async () =>
        {
            var reloaded = await _pageRepository.GetAsync(parent.Id);
            await _pageManager.DeleteAsync(reloaded);
        }));
    }

    /// <summary>
    /// Once the child is moved elsewhere, the same delete that
    /// Should_Reject_Deleting_A_Page_That_Has_Children blocks goes through - the guard checks the live
    /// parent/child relationship at delete time, not some sticky flag set when the child was created.
    /// </summary>
    [Fact]
    public async Task Should_Allow_Deleting_A_Page_After_Its_Child_Is_Moved_Away()
    {
        var parent = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("delete-ok-parent", "Parent", "/delete-ok-parent"));
        var child = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "delete-ok-child", "Child", "/delete-ok-child", parentId: parent.Id));

        await WithUnitOfWorkAsync(async () =>
        {
            var reloadedChild = await _pageRepository.GetAsync(child.Id);
            await _pageManager.MoveAsync(reloadedChild, parentId: null, order: 0);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var reloadedParent = await _pageRepository.GetAsync(parent.Id);
            await _pageManager.DeleteAsync(reloadedParent);
        });

        (await WithUnitOfWorkAsync(() => _pageRepository.FindAsync(parent.Id))).ShouldBeNull();
    }

    /// <summary>
    /// "/" has no meaningful parent, so making a page the home page silently drops whatever parent was
    /// supplied rather than rejecting the combination - see PageManager.NormalizeParent.
    /// </summary>
    [Fact]
    public async Task Should_Clear_The_Parent_When_The_Page_Becomes_The_Home_Page()
    {
        var parent = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("home-parent-test", "Parent", "/home-parent-test"));

        var page = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "home-parent-child", "Child", "/home-parent-child",
            isHomePage: true, parentId: parent.Id));

        page.ParentId.ShouldBeNull();
    }

    /// <summary>
    /// MoveAsync's destination sibling group is densely renumbered to 0..N with the moved page spliced
    /// in - Order has to stay a clean index within siblings, not accumulate gaps every time a page is
    /// dragged around in the Admin UI.
    /// </summary>
    [Fact]
    public async Task Should_Densely_Renumber_Siblings_After_A_Move()
    {
        var parent = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("reorder-parent", "Parent", "/reorder-parent"));

        var first = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "reorder-child-1", "Child 1", "/reorder-child-1", parentId: parent.Id));
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "reorder-child-2", "Child 2", "/reorder-child-2", parentId: parent.Id));
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "reorder-child-3", "Child 3", "/reorder-child-3", parentId: parent.Id));

        // All three share Order 0 at this point, so GetChildrenAsync's Route tie-break puts "first" (the
        // lexicographically earliest route) at index 0. Moving it to index 2 puts it last.
        await WithUnitOfWorkAsync(async () =>
        {
            var reloadedFirst = await _pageRepository.GetAsync(first.Id);
            await _pageManager.MoveAsync(reloadedFirst, parentId: parent.Id, order: 2);
        });

        var siblings = await WithUnitOfWorkAsync(() => _pageRepository.GetChildrenAsync(parent.Id));

        siblings.Select(p => p.Order).OrderBy(order => order).ShouldBe(new[] { 0, 1, 2 });
        siblings.Single(p => p.Id == first.Id).Order.ShouldBe(2);
    }

    /// <summary>
    /// Dragging a page out to the top level in the Admin UI's list calls MoveAsync with a null parent.
    /// Doubles as the regression guard for EF Core's null-semantics translation of
    /// "p.ParentId == parentId" when parentId is itself null - GetChildrenAsync(null) has to mean "the
    /// top-level pages", not "no rows, because nothing equals null".
    /// </summary>
    [Fact]
    public async Task Should_Allow_Moving_A_Page_To_The_Root()
    {
        var parent = await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("promote-parent", "Parent", "/promote-parent"));
        var child = await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "promote-child", "Child", "/promote-child", parentId: parent.Id));

        await WithUnitOfWorkAsync(async () =>
        {
            var reloadedChild = await _pageRepository.GetAsync(child.Id);
            await _pageManager.MoveAsync(reloadedChild, parentId: null, order: 0);
        });

        var reloaded = await WithUnitOfWorkAsync(() => _pageRepository.GetAsync(child.Id));
        reloaded.ParentId.ShouldBeNull();
    }

    /// <summary>
    /// A literal route (/prefix-share-blog) and a deeper templated route
    /// (/prefix-share-blog/{publishTime:yyyy}/{publishTime:MM}/{slug}) are different Route strings that
    /// happen to derive the same own address - RouteExistsAsync only rejects a literal duplicate, so
    /// creating one is never blocked by the other already existing, regardless of order. Which one a
    /// bare "/prefix-share-blog" request actually resolves to is SiteRouteResolver_Tests' concern, not
    /// this uniqueness check's.
    /// </summary>
    [Fact]
    public async Task Should_Allow_A_Literal_Route_To_Coexist_With_A_Templated_Route_Sharing_Its_Own_Address()
    {
        await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("blog-list-literal-first", "Blog list", "/prefix-share-blog"));

        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "blog-detail-templated-second", "Blog detail",
            "/prefix-share-blog/{publishTime:yyyy}/{publishTime:MM}/{slug}"));
    }

    /// <summary>The same pair as above, created in the opposite order - the check has to be symmetric.</summary>
    [Fact]
    public async Task Should_Allow_A_Templated_Route_To_Coexist_With_A_Literal_Route_Created_Afterwards()
    {
        await WithUnitOfWorkAsync(() => _pageManager.CreateAsync(
            "blog-detail-templated-first", "Blog detail",
            "/prefix-share-blog-2/{publishTime:yyyy}/{publishTime:MM}/{slug}"));

        await WithUnitOfWorkAsync(() =>
            _pageManager.CreateAsync("blog-list-literal-second", "Blog list", "/prefix-share-blog-2"));
    }
}
