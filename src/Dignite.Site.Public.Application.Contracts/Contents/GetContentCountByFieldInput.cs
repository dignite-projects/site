using System;
using System.Collections.Generic;
using Dignite.Abp.FlexFields;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// Which contents to count and which field to group them by. The filters are those of
/// <see cref="GetContentListInput"/> and mean the same here - the groups count what that list would show -
/// minus its paging and sorting.
/// </summary>
public class GetContentCountByFieldInput
{
    /// <summary>The field whose values the contents are grouped by. It has to be an indexed one: a field type that is not searchable (rich text, files) has no values to group.</summary>
    public Guid FieldId { get; set; }

    public Guid? PageId { get; set; }

    public string? CultureName { get; set; }

    public Guid? ContentTypeId { get; set; }

    public string? Filter { get; set; }

    /// <summary>Optional lower bound on <c>PublishTime</c>, inclusive - see <see cref="GetContentListInput.PublishedAfter"/>.</summary>
    public DateTime? PublishedAfter { get; set; }

    /// <summary>Optional upper bound on <c>PublishTime</c>, inclusive - capped at "now" like <see cref="GetContentListInput.PublishedBefore"/>.</summary>
    public DateTime? PublishedBefore { get; set; }

    /// <summary>
    /// Narrow the counted contents - pushed down to the typed query index table. To count a facet under the
    /// other filters, leave the grouped field's own condition out.
    /// </summary>
    public List<FlexFieldQueryCondition>? FlexFieldConditions { get; set; }

    /// <summary>The most groups to return, most numerous first. Default <see cref="DefaultMaxResultCount"/>, at most <see cref="MaxAllowedMaxResultCount"/>.</summary>
    public int MaxResultCount { get; set; } = DefaultMaxResultCount;

    public const int DefaultMaxResultCount = 100;

    public const int MaxAllowedMaxResultCount = 1000;
}
