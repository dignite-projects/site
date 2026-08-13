using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Pages;

public class EfCorePageRepository : EfCoreRepository<ISiteDbContext, Page, Guid>, IPageRepository
{
    public EfCorePageRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<Page?> FindByNameAsync(
        string name,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .IncludeDetails(includeDetails)
            .FirstOrDefaultAsync(p => p.Name == name, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Page?> FindByPathAsync(
        string path,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        var normalized = Page.NormalizeRoute(path);

        // A page's route may be a template (/blog/{slug}) whose own address (/blog) is a shorter, derived
        // string - so a literal Route match alone would miss it. The SQL side is a coarse net (anything
        // that could possibly derive to this address); PageRoute.GetPath in memory is the actual judge -
        // one place decides both directions, or "what is this page's address" could start disagreeing
        // with itself the way SiteUrlBuilder used to before it read GetPath() too.
        var prefix = normalized == "/" ? "/" : normalized + "/";

        var candidates = await (await GetQueryableAsync())
            .IncludeDetails(includeDetails)
            .Where(p => p.Route == normalized || p.Route.StartsWith(prefix))
            .ToListAsync(GetCancellationToken(cancellationToken));

        // More than one page can derive the same own address - a literal /blog and a templated
        // /blog/{publishTime:yyyy}/{publishTime:MM}/{slug} both claim /blog, and neither creation is
        // rejected for it (RouteExistsAsync only rejects a literal Route duplicate). A literal route
        // always wins that address deterministically; among templated routes alone the tie-break is
        // just a stable order, the same "admin's problem" territory GetRoutableListAsync already
        // documents for two templates that overlap on purpose.
        return candidates
            .Where(p => PageRoute.GetPath(p.Route) == normalized)
            .OrderBy(p => PageRoute.IsTemplate(p.Route) ? 1 : 0)
            .ThenBy(p => p.Route, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public virtual async Task<bool> RouteExistsAsync(
        string route,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Page.NormalizeRoute(route);

        // A literal string duplicate, nothing more - /blog and /blog/{slug} are different strings that
        // may both derive the same own address (see FindByPathAsync), and that is not a conflict: only
        // one of them can ever win that address, deterministically, at resolution time, so there is
        // nothing here for creation-time uniqueness to protect against.
        return await (await GetDbSetAsync()).AsNoTracking()
            .AnyAsync(
                p => (excludedId == null || p.Id != excludedId) && p.Route == normalized,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> NameExistsAsync(
        string name,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(
                p => p.Name == name && (excludedId == null || p.Id != excludedId),
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Page?> FindHomePageAsync(
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        // Not a flag to filter by in SQL - a page is the home page when PageRoute.IsHomeRoute(p.Route) is
        // true, and that is string-slicing logic EF Core cannot translate, the same reason FindByPathAsync
        // judges its own address in memory rather than in the query. More than one page can pass it - an
        // admin could have both "/" and "/{slug?}" in the table at once - so the same tie-break
        // FindByPathAsync applies to a shared address applies here too: a literal route beats a template,
        // then alphabetically by Route, so there is always at most one winner.
        var candidates = await (await GetQueryableAsync())
            .IncludeDetails(includeDetails)
            .ToListAsync(GetCancellationToken(cancellationToken));

        return candidates
            .Where(p => p.IsHomeRoute())
            .OrderBy(p => PageRoute.IsTemplate(p.Route) ? 1 : 0)
            .ThenBy(p => p.Route, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public virtual async Task<List<Page>> GetRoutableListAsync(CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(p => p.IsActive)
            .OrderBy(p => p.Route)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Page>> GetListAsync(
        bool? isActive = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetFilteredQueryableAsync(isActive, filter))
            .OrderBy(p => p.Route)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Page>> GetListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> AnyChildAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(p => p.ParentId == parentId, GetCancellationToken(cancellationToken));
    }

    protected virtual async Task<IQueryable<Page>> GetFilteredQueryableAsync(bool? isActive, string? filter)
    {
        return (await GetDbSetAsync())
            .WhereIf(isActive.HasValue, p => p.IsActive == isActive!.Value)
            .WhereIf(
                !filter.IsNullOrWhiteSpace(),
                p => p.Name.Contains(filter!) || p.DisplayName.Contains(filter!) || p.Route.Contains(filter!));
    }

    public override async Task<IQueryable<Page>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).IncludeDetails();
    }
}

public static class PageRepositoryQueryableExtensions
{
    public static IQueryable<Page> IncludeDetails(this IQueryable<Page> queryable, bool include = true)
    {
        return include ? queryable.Include(p => p.ContentTypes) : queryable;
    }
}
