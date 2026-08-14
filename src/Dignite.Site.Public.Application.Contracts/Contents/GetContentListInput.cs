using System;
using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// No status filter - the service forces "published as of now" unconditionally, so exposing one here
/// would only invite a caller to ask for drafts. <see cref="PublishedAfter"/>/<see cref="PublishedBefore"/>
/// are the one exception, and only because they can never widen that window, only narrow it further - see
/// their own remarks.
/// </summary>
public class GetContentListInput : PagedAndSortedResultRequestDto
{
    public Guid? PageId { get; set; }

    public string? CultureName { get; set; }

    public Guid? ContentTypeId { get; set; }

    public string? Filter { get; set; }

    /// <summary>
    /// Optional lower bound on <c>PublishTime</c>, inclusive - e.g. for a route-filtered "archive" list
    /// (总体设计 §3.4). Safe to expose here unconditionally: a caller can only narrow "published as of now"
    /// further, never widen it, since a lower bound cannot reveal anything not already visible.
    /// </summary>
    public DateTime? PublishedAfter { get; set; }

    /// <summary>
    /// Optional upper bound on <c>PublishTime</c>, inclusive. Unlike <see cref="PublishedAfter"/>, this one
    /// *could* widen visibility past "now" if honored as given (a caller asking for everything published
    /// before next month would otherwise see a content scheduled for later this week) - the service caps
    /// it at "now" regardless of what is passed here, so this can never surface a scheduled-future content
    /// ahead of its own <c>PublishTime</c>.
    /// </summary>
    public DateTime? PublishedBefore { get; set; }

    /// <summary>Pushed down to the typed query index table and never evaluated in memory (总体设计 §2.4).</summary>
    public List<FlexFieldQueryCondition>? FlexFieldConditions { get; set; }
}
