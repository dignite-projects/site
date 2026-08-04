using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Sites.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Sites.Pages;

public class EfCorePageRepository : EfCoreRepository<ISitesDbContext, Page, Guid>, IPageRepository
{
    public EfCorePageRepository(IDbContextProvider<ISitesDbContext> dbContextProvider)
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

    public virtual async Task<Page?> FindByRouteAsync(
        string route,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        var normalized = Page.NormalizeRoute(route);

        return await (await GetQueryableAsync())
            .IncludeDetails(includeDetails)
            .FirstOrDefaultAsync(p => p.Route == normalized, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> RouteExistsAsync(
        string route,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Page.NormalizeRoute(route);

        return await (await GetDbSetAsync())
            .AnyAsync(
                p => p.Route == normalized && (excludedId == null || p.Id != excludedId),
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
        return await (await GetQueryableAsync())
            .IncludeDetails(includeDetails)
            .FirstOrDefaultAsync(p => p.IsHomePage, GetCancellationToken(cancellationToken));
    }

    /// <summary>
    /// Longest route first. Route resolution walks this list testing prefixes, and <c>/blog</c> is a
    /// prefix of <c>/blog-archive</c> - shorter-first would let the blog page claim the archive's URLs.
    /// </summary>
    public virtual async Task<List<Page>> GetRoutableListAsync(CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Route.Length)
            .ThenBy(p => p.Route)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Page>> GetListAsync(
        bool? isActive = null,
        string? filter = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        string? sorting = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetFilteredQueryableAsync(isActive, filter))
            .OrderBy(sorting.IsNullOrWhiteSpace() ? $"{nameof(Page.Order)} asc,{nameof(Page.Route)} asc" : sorting!)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<int> GetCountAsync(
        bool? isActive = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetFilteredQueryableAsync(isActive, filter))
            .CountAsync(GetCancellationToken(cancellationToken));
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
