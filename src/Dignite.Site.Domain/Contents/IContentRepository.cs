using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Site.Contents;

public interface IContentRepository : IBasicRepository<Content, Guid>
{
    /// <summary>
    /// The unique-constraint lookup: at most one content per <c>(PageId, CultureName, Slug)</c>
    /// (总体设计 §2.5). This is step 2 of route resolution, and the whole reason a slug has to be
    /// unique within its page and language.
    /// </summary>
    Task<Content?> FindBySlugAsync(
        Guid pageId,
        string cultureName,
        string slug,
        CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(
        Guid pageId,
        string cultureName,
        string slug,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every language version of one content, found by the natural key that groups them
    /// <c>(PageId, ContentTypeId, Slug)</c> - the source for hreflang and the language switcher
    /// (总体设计 §5.5). There is no group id; this <i>is</i> the grouping.
    /// </summary>
    Task<List<Content>> GetTranslationsAsync(
        Guid pageId,
        Guid contentTypeId,
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ordered by id, for <c>IFlexFieldProvider.GetPagedEntitiesAsync</c>. Separate from
    /// <see cref="GetListAsync"/> because a rebuild needs a stable order above all else, whereas a list
    /// view needs a meaningful one.
    /// </summary>
    Task<List<Content>> GetPagedListByIdAsync(
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The general list query.
    /// <para>
    /// <paramref name="flexFieldConditions"/> is pushed down to SQL through
    /// <c>IFlexFieldQueryExecutor&lt;Content&gt;</c> against the typed index table - never evaluated in
    /// memory. That is the specific fix for Dignite.Cms's <c>EfCoreEntryRepository.GetListAsync</c>,
    /// which called <c>AsEnumerable()</c> as soon as a custom-field filter was present and then scanned
    /// the whole table.
    /// </para>
    /// </summary>
    Task<List<Content>> GetListAsync(
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
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(
        Guid? pageId = null,
        string? cultureName = null,
        Guid? contentTypeId = null,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        DateTime? publishedAfter = null,
        string? filter = null,
        IReadOnlyList<FlexFieldQueryCondition>? flexFieldConditions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any content still uses <paramref name="contentTypeId"/> - checked before deleting a
    /// content type.
    /// </summary>
    Task<bool> AnyByContentTypeAsync(Guid contentTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which languages have at least one matching content under <paramref name="pageId"/> - a page's
    /// language footprint for hreflang (总体设计 §5.5), without materializing every content row (and its
    /// full FlexFields bag) just to project one column. Pushed to SQL as <c>SELECT DISTINCT</c>.
    /// </summary>
    Task<List<string>> GetDistinctCultureNamesAsync(
        Guid pageId,
        ContentStatus? status = null,
        DateTime? publishedBefore = null,
        CancellationToken cancellationToken = default);
}
