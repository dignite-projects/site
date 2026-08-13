using System;
using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// No status or publish-window filter - the service forces "published as of now" unconditionally, so
/// exposing those here would only invite a caller to ask for drafts.
/// </summary>
public class GetContentListInput : PagedAndSortedResultRequestDto
{
    public Guid? PageId { get; set; }

    public string? CultureName { get; set; }

    public Guid? ContentTypeId { get; set; }

    public string? Filter { get; set; }

    /// <summary>Pushed down to the typed query index table and never evaluated in memory (总体设计 §2.4).</summary>
    public List<FlexFieldQueryCondition>? FlexFieldConditions { get; set; }
}
