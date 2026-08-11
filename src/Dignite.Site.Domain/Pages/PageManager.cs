using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Pages;

/// <summary>
/// Creating and changing pages, with the uniqueness rules that keep the routing table unambiguous
/// (总体设计 §3.1), and the parent/child rules that keep the Admin UI's page tree a tree.
/// </summary>
public class PageManager : DomainService
{
    protected IPageRepository PageRepository { get; }

    protected IContentTypeRepository ContentTypeRepository { get; }

    protected IContentRepository ContentRepository { get; }

    public PageManager(
        IPageRepository pageRepository,
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository)
    {
        PageRepository = pageRepository;
        ContentTypeRepository = contentTypeRepository;
        ContentRepository = contentRepository;
    }

    /// <summary>
    /// Deletes a page and everything beneath it - every content type defined on it, and every content of
    /// those types in every language (总体设计 §2.5). <b>Refuses when the page has child pages</b>
    /// (<see cref="PageHasChildrenException"/>) rather than taking them with it - unlike content types and
    /// contents, child pages are not this page's own furniture, and each one can carry its own content
    /// types and contents, so cascading through them would multiply the blast radius of one delete far
    /// past what a confirmation dialog can convey. Move or delete the children first.
    /// <para>
    /// <b>The descendants are deleted here, explicitly, and that is not redundant with the database's
    /// cascading foreign keys.</b> These entities are all <c>FullAuditedAggregateRoot</c>, so ABP
    /// soft-deletes them: a delete is an <c>UPDATE ... SET IsDeleted = 1</c>, and a cascade declared
    /// <c>ON DELETE CASCADE</c> fires on <c>DELETE</c> only. Relying on it left the page hidden while every
    /// content type and content under it stayed live - unreachable through any name-addressed surface,
    /// since those resolve through the page, yet still present to anything enumerating contents directly.
    /// </para>
    /// <para>
    /// Contents go before their content types, because the ordinary delete path refuses to remove a
    /// content type that still has contents - a guard that is right for deleting one type on its own and
    /// wrong here, where the whole section is going.
    /// </para>
    /// </summary>
    public virtual async Task DeleteAsync(Page page, CancellationToken cancellationToken = default)
    {
        if (await PageRepository.AnyChildAsync(page.Id, cancellationToken))
        {
            throw new PageHasChildrenException(page.Name);
        }

        var contentTypes = await ContentTypeRepository.GetListByPageAsync(page.Id, cancellationToken);

        foreach (var contentType in contentTypes)
        {
            var contents = await ContentRepository.GetListAsync(
                pageId: page.Id, contentTypeId: contentType.Id, cancellationToken: cancellationToken);

            foreach (var content in contents)
            {
                await ContentRepository.DeleteAsync(content, cancellationToken: cancellationToken);
            }

            await ContentTypeRepository.DeleteAsync(contentType, cancellationToken: cancellationToken);
        }

        await PageRepository.DeleteAsync(page, cancellationToken: cancellationToken);
    }

    public virtual async Task<Page> CreateAsync(
        string name,
        string displayName,
        string route,
        string? template = null,
        bool isHomePage = false,
        bool isActive = true,
        Guid? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoute = Page.NormalizeRoute(route);
        var effectiveParentId = NormalizeParent(isHomePage, parentId);

        // Checked before the database round trips below - a malformed route is cheap to catch and does
        // not need a uniqueness query to have already run first.
        if (!PageRoute.IsValid(normalizedRoute))
        {
            throw new InvalidPageRouteException(normalizedRoute);
        }

        await CheckNameAsync(name, null, cancellationToken);
        await CheckRouteAsync(normalizedRoute, null, cancellationToken);
        await CheckParentAsync(null, effectiveParentId, cancellationToken);

        var page = new Page(
            id: GuidGenerator.Create(),
            name: name,
            displayName: displayName,
            route: normalizedRoute,
            template: template,
            isHomePage: isHomePage,
            isActive: isActive,
            tenantId: CurrentTenant.Id,
            parentId: effectiveParentId);

        if (isHomePage)
        {
            await DemoteExistingHomePageAsync(page.Id, cancellationToken);
        }

        return await PageRepository.InsertAsync(page, cancellationToken: cancellationToken);
    }

    public virtual async Task<Page> UpdateAsync(
        Page page,
        string name,
        string displayName,
        string route,
        string? template = null,
        bool isHomePage = false,
        bool isActive = true,
        Guid? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoute = Page.NormalizeRoute(route);
        // Computed up front, before IsHomePage is set below: NormalizeParent decides purely from the
        // isHomePage argument (the new, incoming value), never from page.IsHomePage, so evaluating it
        // early costs nothing - but doing so also means CheckParentAsync never walks an ancestor chain
        // that is about to be discarded anyway when the page is becoming the home page.
        var effectiveParentId = NormalizeParent(isHomePage, parentId);

        if (!string.Equals(page.Name, name, StringComparison.Ordinal))
        {
            await CheckNameAsync(name, page.Id, cancellationToken);
            page.SetName(name);
        }

        if (!string.Equals(page.Route, normalizedRoute, StringComparison.Ordinal))
        {
            // Checked before the database round trip below, same reasoning as CreateAsync.
            if (!PageRoute.IsValid(normalizedRoute))
            {
                throw new InvalidPageRouteException(normalizedRoute);
            }

            await CheckRouteAsync(normalizedRoute, page.Id, cancellationToken);
            page.SetRoute(normalizedRoute);
        }

        if (page.ParentId != effectiveParentId)
        {
            await CheckParentAsync(page.Id, effectiveParentId, cancellationToken);
            page.SetParent(effectiveParentId);
        }

        page.SetDisplayName(displayName);
        page.SetTemplate(template);
        page.SetIsActive(isActive);

        if (isHomePage && !page.IsHomePage)
        {
            await DemoteExistingHomePageAsync(page.Id, cancellationToken);
        }

        page.SetIsHomePage(isHomePage);

        return await PageRepository.UpdateAsync(page, cancellationToken: cancellationToken);
    }

    protected virtual async Task CheckNameAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await PageRepository.NameExistsAsync(name, excludedId, cancellationToken))
        {
            throw new PageNameAlreadyExistException(name);
        }
    }

    protected virtual async Task CheckRouteAsync(string route, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await PageRepository.RouteExistsAsync(route, excludedId, cancellationToken))
        {
            throw new PageRouteAlreadyExistException(route);
        }
    }

    /// <summary>
    /// The home page is always the tree's root - <c>"/"</c> has no meaningful parent - so a request to
    /// make a page the home page silently drops whatever parent was supplied, rather than rejecting the
    /// combination as an error.
    /// </summary>
    protected virtual Guid? NormalizeParent(bool isHomePage, Guid? parentId)
    {
        return isHomePage ? null : parentId;
    }

    /// <summary>
    /// Confirms a candidate parent exists, and that assigning it would not create a cycle: walking its own
    /// chain of parents back up must never reach <paramref name="pageId"/>. <paramref name="pageId"/> is
    /// null when a page is still being created, which can be skipped past existence - a brand new id
    /// cannot appear as anyone's ancestor yet.
    /// </summary>
    protected virtual async Task CheckParentAsync(Guid? pageId, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId == null)
        {
            return;
        }

        // Confirms existence as a side effect - a non-existent id surfaces as the standard
        // EntityNotFoundException rather than a bespoke "parent not found" error.
        var parent = await PageRepository.GetAsync(parentId.Value, includeDetails: false, cancellationToken);

        if (pageId == null)
        {
            return;
        }

        if (parentId == pageId)
        {
            throw new PageParentCycleException(parent.Name);
        }

        // Guards a pre-existing cycle upstream that does not involve pageId - not this call's problem to
        // report, just something to not loop forever over.
        var visited = new HashSet<Guid>();
        var current = parent;

        while (current.ParentId != null)
        {
            if (current.ParentId == pageId)
            {
                throw new PageParentCycleException(parent.Name);
            }

            if (!visited.Add(current.ParentId.Value))
            {
                break;
            }

            current = await PageRepository.FindAsync(current.ParentId.Value, includeDetails: false, cancellationToken);
            if (current == null)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Clears the flag on whatever page currently holds it. Enforced rather than merely validated,
    /// because <c>x-default</c> in the hreflang set (总体设计 §5.5) has to resolve to exactly one page -
    /// a site with two home pages emits contradictory alternates, and one with none emits no
    /// <c>x-default</c> at all.
    /// </summary>
    protected virtual async Task DemoteExistingHomePageAsync(Guid newHomePageId, CancellationToken cancellationToken)
    {
        var current = await PageRepository.FindHomePageAsync(cancellationToken: cancellationToken);

        if (current != null && current.Id != newHomePageId)
        {
            current.SetIsHomePage(false);
            await PageRepository.UpdateAsync(current, cancellationToken: cancellationToken);
        }
    }
}
