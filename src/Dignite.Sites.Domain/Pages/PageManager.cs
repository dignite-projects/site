using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace Dignite.Sites.Pages;

/// <summary>
/// Creating and changing pages, with the uniqueness rules that keep the routing table unambiguous
/// (总体设计 §3.1).
/// </summary>
public class PageManager : DomainService
{
    protected IPageRepository PageRepository { get; }

    public PageManager(IPageRepository pageRepository)
    {
        PageRepository = pageRepository;
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
