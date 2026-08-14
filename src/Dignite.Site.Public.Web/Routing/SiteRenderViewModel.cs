using System;
using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Dignite.Site.Public.Seo;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// The @model every <c>Views/**/*.cshtml</c> template receives - one shape covering every non-None
/// <c>RouteMatchKindDto</c> outcome. <c>Page.Template</c> and <c>Page.ContentTemplate</c> resolve to two
/// independent view files (<see cref="Content"/> null vs. populated) rather than one view branching
/// internally - see <c>SiteRenderController.RenderAsync</c>.
/// </summary>
public class SiteRenderViewModel
{
    public required PageDto Page { get; init; }

    public required string CultureName { get; init; }

    /// <summary>
    /// Placeholders a partial page-route match resolved short of a slug, minus <c>PublishTime</c> - see
    /// <see cref="PublishedAfter"/>. Forwarded as-is to <c>ContentListTagHelper.FieldFilters</c>, which
    /// resolves each entry against FlexFields.
    /// </summary>
    public required IReadOnlyDictionary<string, string> FieldFilters { get; init; }

    /// <summary>
    /// Lower bound carved out of a <c>publishTime</c> route placeholder, if the match captured one - see
    /// <c>SiteRenderFilterValueMapper</c>. <c>PublishTime</c> is <c>Content</c>'s own system field, not a
    /// FlexFields field (总体设计 §2.4), so it never appears in <see cref="FieldFilters"/> itself.
    /// </summary>
    public DateTime? PublishedAfter { get; init; }

    /// <summary>Upper bound carved out of a <c>publishTime</c> route placeholder - see <see cref="PublishedAfter"/>.</summary>
    public DateTime? PublishedBefore { get; init; }

    public HeadMetadataDto? HeadMetadata { get; init; }

    /// <summary>Populated for RouteMatchKindDto.ContentOfPage / .Content. Null for RouteMatchKindDto.Page - a front end queries a list itself (总体设计 §7's "rendering is handed to the front end"), this no longer pre-fetches one.</summary>
    public ContentRenderViewModel? Content { get; init; }
}

/// <summary>One content, full detail - every non-Seo field the content type declares.</summary>
public class ContentRenderViewModel
{
    public required ContentDto Content { get; init; }

    public required ContentTypeDto ContentType { get; init; }

    public required IReadOnlyList<FlexFieldValue> Fields { get; init; }
}
