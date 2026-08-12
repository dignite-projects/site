using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Dignite.Site.Public.Seo;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// The @model every <c>Views/**/*.cshtml</c> template receives - one shape covering every non-None
/// <c>RouteMatchKindDto</c> outcome, so one <c>Page.Template</c> value resolves to one view file
/// regardless of whether the request named the page itself (a list) or a piece of content beneath it.
/// </summary>
public class SiteRenderViewModel
{
    public required PageDto Page { get; init; }

    public required string CultureName { get; init; }

    /// <summary>Placeholders a partial page-route match resolved short of a slug. Display only in v1 - not used to filter <see cref="List"/>'s query.</summary>
    public required IReadOnlyDictionary<string, string> FilterValues { get; init; }

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
