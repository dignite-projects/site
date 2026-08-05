using System;
using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Dignite.Site.Contents;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Contents;

public class GetContentListInput : PagedAndSortedResultRequestDto
{
    public Guid? PageId { get; set; }

    public string? CultureName { get; set; }

    public Guid? ContentTypeId { get; set; }

    public ContentStatus? Status { get; set; }

    public DateTime? PublishedBefore { get; set; }

    public DateTime? PublishedAfter { get; set; }

    public string? Filter { get; set; }

    /// <summary>
    /// Pushed down to the typed query index table and never evaluated in memory - the specific fix
    /// 总体设计 §2.4 calls out for Dignite.Cms's worst query path.
    /// </summary>
    public List<FlexFieldQueryCondition>? FlexFieldConditions { get; set; }
}
