using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
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

        // Id breaks PublishTime ties, so the default order is deterministic and agrees with
        // FindPreviousAsync/FindNextAsync - a detail page's previous/next links walk exactly this list.
        return await query
            .OrderBy(sorting.IsNullOrWhiteSpace()
                ? $"{nameof(Content.PublishTime)} desc, {nameof(Content.Id)} desc"
                : sorting!)
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

    public virtual async Task<List<ContentFieldValueCount>> GetCountByFieldValueAsync(
        Guid fieldId,
        FlexFieldValueType valueType,
        Guid? pageId = null,
        string? cultureName = null,
        Guid? contentTypeId = null,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        DateTime? publishedAfter = null,
        string? filter = null,
        IReadOnlyList<FlexFieldQueryCondition>? flexFieldConditions = null,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        // The contents the list would show, as a subquery on the index rows - the same filter, so a count
        // can never disagree with the list it sits beside about which contents are in play.
        var contentIds = (await GetFilteredQueryableAsync(
                pageId, cultureName, contentTypeId, status, publishedBefore, publishedAfter, filter,
                flexFieldConditions, cancellationToken))
            .Select(c => c.Id);

        var index = (await GetDbContextAsync()).ContentFlexFieldIndexes
            .Where(i => i.FieldId == fieldId && i.ValueType == valueType && contentIds.Contains(i.ContentId));

        var token = GetCancellationToken(cancellationToken);

        return valueType switch
        {
            FlexFieldValueType.String => await CountByValueAsync(index, i => i.StringValue, v => v, maxResultCount, token),
            FlexFieldValueType.Number => await CountByValueAsync(
                index, i => i.NumberValue, v => v == null ? null : FormatNumber(v.Value), maxResultCount, token),
            FlexFieldValueType.DateTime => await CountByValueAsync(
                index, i => i.DateTimeValue, v => v?.ToString("O", CultureInfo.InvariantCulture), maxResultCount, token),
            FlexFieldValueType.Boolean => await CountByValueAsync(
                index, i => i.BooleanValue, v => v == null ? null : v.Value ? "true" : "false", maxResultCount, token),
            FlexFieldValueType.Guid => await CountByValueAsync(
                index, i => i.GuidValue, v => v?.ToString("D"), maxResultCount, token),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType), valueType, null)
        };
    }

    /// <summary>
    /// A number without the trailing zeros its column's scale may carry: 42 and 42.0 are one value, and the
    /// group's text is <c>42</c> either way.
    /// </summary>
    private static string FormatNumber(decimal number)
    {
        return (number / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Groups one field's index rows by the typed column <paramref name="value"/> names. A distinct count of
    /// <c>ContentId</c>, not of rows: a content repeating a value is still one content.
    /// </summary>
    private static async Task<List<ContentFieldValueCount>> CountByValueAsync<TValue>(
        IQueryable<ContentFlexFieldIndex> index,
        Expression<Func<ContentFlexFieldIndex, TValue>> value,
        Func<TValue, string?> format,
        int maxResultCount,
        CancellationToken cancellationToken)
    {
        // Ordered before it is projected: EF Core cannot order by a member of a constructor projection.
        var groups = index
            .GroupBy(value)
            .OrderByDescending(g => g.Select(i => i.ContentId).Distinct().Count())
            .ThenBy(g => g.Key)
            .Select(g => new ValueGroup<TValue>(g.Key, g.Select(i => i.ContentId).Distinct().Count()));

        var limited = maxResultCount < int.MaxValue ? groups.Take(maxResultCount) : groups;

        var result = new List<ContentFieldValueCount>();
        foreach (var group in await limited.ToListAsync(cancellationToken))
        {
            // A row of this value type always has its column set; a null key is not a value to count.
            if (format(group.Value) is { } text)
            {
                result.Add(new ContentFieldValueCount(text, group.Count));
            }
        }

        return result;
    }

    private sealed record ValueGroup<TValue>(TValue Value, int Count);

    public virtual async Task<Content?> FindPreviousAsync(
        Guid pageId,
        string cultureName,
        DateTime publishTime,
        Guid id,
        Guid? contentTypeId = null,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetFilteredQueryableAsync(
            pageId, cultureName, contentTypeId, status, publishedBefore, null, null, null, cancellationToken);

        return await query
            .Where(c => c.PublishTime < publishTime || (c.PublishTime == publishTime && c.Id.CompareTo(id) < 0))
            .OrderByDescending(c => c.PublishTime)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Content?> FindNextAsync(
        Guid pageId,
        string cultureName,
        DateTime publishTime,
        Guid id,
        Guid? contentTypeId = null,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetFilteredQueryableAsync(
            pageId, cultureName, contentTypeId, status, publishedBefore, null, null, null, cancellationToken);

        return await query
            .Where(c => c.PublishTime > publishTime || (c.PublishTime == publishTime && c.Id.CompareTo(id) > 0))
            .OrderBy(c => c.PublishTime)
            .ThenBy(c => c.Id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
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
