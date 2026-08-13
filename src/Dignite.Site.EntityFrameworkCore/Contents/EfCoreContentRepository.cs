using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Contents;

public class EfCoreContentRepository : EfCoreRepository<ISiteDbContext, Content, Guid>, IContentRepository
{
    protected IFlexFieldQueryExecutor<Content> FlexFieldQueryExecutor { get; }

    public EfCoreContentRepository(
        IDbContextProvider<ISiteDbContext> dbContextProvider,
        IFlexFieldQueryExecutor<Content> flexFieldQueryExecutor)
        : base(dbContextProvider)
    {
        FlexFieldQueryExecutor = flexFieldQueryExecutor;
    }

    public virtual async Task<Content?> FindBySlugAsync(
        Guid pageId,
        string cultureName,
        string slug,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(
                c => c.PageId == pageId && c.CultureName == cultureName && c.Slug == slug,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> SlugExistsAsync(
        Guid pageId,
        string cultureName,
        string slug,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(
                c => c.PageId == pageId
                     && c.CultureName == cultureName
                     && c.Slug == slug
                     && (excludedId == null || c.Id != excludedId),
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Content>> GetTranslationsAsync(
        Guid pageId,
        Guid contentTypeId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(c => c.PageId == pageId && c.ContentTypeId == contentTypeId && c.Slug == slug)
            .OrderBy(c => c.CultureName)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Content>> GetPagedListOrderByIdAsync(
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .OrderBy(c => c.Id)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Content>> GetListAsync(
        Guid? pageId = null,
        string? cultureName = null,
        Guid? contentTypeId = null,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        DateTime? publishedAfter = null,
        string? filter = null,
        IReadOnlyList<FlexFieldQueryCondition>? flexFieldConditions = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        string? sorting = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetFilteredQueryableAsync(
            pageId, cultureName, contentTypeId, status, publishedBefore, publishedAfter, filter,
            flexFieldConditions, cancellationToken);

        return await query
            .OrderBy(sorting.IsNullOrWhiteSpace() ? $"{nameof(Content.PublishTime)} desc" : sorting!)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<int> GetCountAsync(
        Guid? pageId = null,
        string? cultureName = null,
        Guid? contentTypeId = null,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        DateTime? publishedAfter = null,
        string? filter = null,
        IReadOnlyList<FlexFieldQueryCondition>? flexFieldConditions = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetFilteredQueryableAsync(
            pageId, cultureName, contentTypeId, status, publishedBefore, publishedAfter, filter,
            flexFieldConditions, cancellationToken);

        return await query.CountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> AnyByContentTypeAsync(
        Guid contentTypeId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(c => c.ContentTypeId == contentTypeId, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<string>> GetDistinctCultureNamesAsync(
        Guid pageId,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetFilteredQueryableAsync(
            pageId, null, null, status, publishedBefore, null, null, null, cancellationToken);

        return await query
            .Select(c => c.CultureName)
            .Distinct()
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    /// <summary>
    /// Builds the shared filter for the list and count queries.
    /// <para>
    /// The field conditions are handed to the kernel's query executor, which turns each into a subquery
    /// over the typed index table and composes it onto this one. The result is still an
    /// <see cref="IQueryable{T}"/> - nothing is enumerated here, so paging and counting still happen in
    /// the database. That is the whole point: Dignite.Cms's equivalent called <c>AsEnumerable()</c> at
    /// this exact spot and filtered the table in memory.
    /// </para>
    /// </summary>
    protected virtual async Task<IQueryable<Content>> GetFilteredQueryableAsync(
        Guid? pageId,
        string? cultureName,
        Guid? contentTypeId,
        ContentStatus? status,
        DateTime? publishedBefore,
        DateTime? publishedAfter,
        string? filter,
        IReadOnlyList<FlexFieldQueryCondition>? flexFieldConditions,
        CancellationToken cancellationToken)
    {
        // TryNormalize, not the strict overload. This is a filter, and a culture no CultureInfo
        // recognizes cannot be the culture of any stored row - every write path normalizes before
        // storing - so the honest answer is "nothing matches", not an ArgumentException. The strict form
        // belongs on the write paths, where an unrecognized tag really is a caller error; throwing out of
        // a query instead surfaces to an API caller as a 500 and to an MCP client as an opaque internal
        // error it cannot correct, over what is usually a one-token typo like "english".
        var hasUnmatchableCulture = false;
        string? normalizedCulture = null;

        if (!cultureName.IsNullOrWhiteSpace())
        {
            hasUnmatchableCulture = !CultureNameNormalizer.TryNormalize(cultureName!, out normalizedCulture);
        }

        var query = (await GetDbSetAsync())
            .WhereIf(hasUnmatchableCulture, c => false)
            .WhereIf(pageId.HasValue, c => c.PageId == pageId!.Value)
            .WhereIf(!hasUnmatchableCulture && normalizedCulture != null, c => c.CultureName == normalizedCulture)
            .WhereIf(contentTypeId.HasValue, c => c.ContentTypeId == contentTypeId!.Value)
            .WhereIf(status.HasValue, c => c.Status == status!.Value)
            .WhereIf(publishedBefore.HasValue, c => c.PublishTime <= publishedBefore!.Value)
            .WhereIf(publishedAfter.HasValue, c => c.PublishTime >= publishedAfter!.Value)
            .WhereIf(!filter.IsNullOrWhiteSpace(), c => c.Slug.Contains(filter!));

        if (flexFieldConditions is { Count: > 0 })
        {
            query = await FlexFieldQueryExecutor.ApplyFilterAsync(query, flexFieldConditions, cancellationToken);
        }

        return query;
    }
}
