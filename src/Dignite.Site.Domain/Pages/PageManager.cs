using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Pages;

/// <summary>
/// Creating and changing pages, with the uniqueness rules that keep the routing table unambiguous
/// (总体设计 §3.1).
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
    /// those types in every language (总体设计 §2.5).
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
        string? contentPathPattern = null,
        string? template = null,
        bool isHomePage = false,
        int order = 0,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoute = Page.NormalizeRoute(route);

        await CheckNameAsync(name, null, cancellationToken);
        await CheckRouteAsync(normalizedRoute, null, cancellationToken);

        var page = new Page(
            GuidGenerator.Create(),
            name,
            displayName,
            normalizedRoute,
            contentPathPattern,
            template,
            isHomePage,
            order,
            isActive,
            CurrentTenant.Id);

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
        string? contentPathPattern = null,
        string? template = null,
        bool isHomePage = false,
        int order = 0,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoute = Page.NormalizeRoute(route);

        if (!string.Equals(page.Name, name, StringComparison.Ordinal))
        {
            await CheckNameAsync(name, page.Id, cancellationToken);
            page.SetName(name);
        }

        if (!string.Equals(page.Route, normalizedRoute, StringComparison.Ordinal))
        {
            await CheckRouteAsync(normalizedRoute, page.Id, cancellationToken);
            page.SetRoute(normalizedRoute);
        }

        page.SetDisplayName(displayName);
        page.SetContentPathPattern(contentPathPattern);
        page.SetTemplate(template);
        page.SetOrder(order);
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
