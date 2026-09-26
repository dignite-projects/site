using System.Collections.Generic;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Pages;

namespace Dignite.Site.Routing;

/// <summary>
/// What a request path resolved to (总体设计 §3.4). The back end answers "which page, which content, what
/// shape"; deciding whether that renders as a single page, a list or a detail view is the front end's
/// call, which is why there is no page-kind here to read.
/// </summary>
public class RouteMatch
{
    private static readonly IReadOnlyDictionary<string, string> NoFilterValues = new Dictionary<string, string>();

    private RouteMatch(
        RouteMatchKind kind,
        Page? page = null,
        Content? content = null,
        ContentType? contentType = null,
        IReadOnlyDictionary<string, string>? filterValues = null,
        bool isTruncated = false)
    {
        Kind = kind;
        Page = page;
        Content = content;
        ContentType = contentType;
        FilterValues = filterValues ?? NoFilterValues;
        IsTruncated = isTruncated;
    }

    public RouteMatchKind Kind { get; }

    /// <summary>The matched page. Null only when <see cref="Kind"/> is <see cref="RouteMatchKind.None"/>.</summary>
    public Page? Page { get; }

    /// <summary>The matched content, when the path carried a slug.</summary>
    public Content? Content { get; }

    /// <summary>The matched content's shape - loaded alongside it, since a renderer needs it immediately.</summary>
    public ContentType? ContentType { get; }

    /// <summary>
    /// The values a request path named for <see cref="Page"/>'s placeholders short of a slug - e.g.
    /// <c>{"publishTime:yyyy-MM": "2026-08"}</c> for <c>/blog/2026-08</c> against
    /// <c>/blog/{publishTime:yyyy-MM}/{slug}</c> (总体设计 §3.4, <see cref="PageRoute.TryMatchPartial"/>), or
    /// for every placeholder of a route with no slug at all (<see cref="PageRoute.TryMatchExact"/>) - see
    /// <see cref="IsTruncated"/> for telling the two apart. Empty - never null - whenever
    /// <see cref="Kind"/> is <see cref="RouteMatchKind.Page"/> from a bare address instead: a front end
    /// that only ever checks <see cref="Kind"/> and reads this when convenient does not have to
    /// special-case which.
    /// </summary>
    public IReadOnlyDictionary<string, string> FilterValues { get; }

    /// <summary>
    /// Whether <see cref="FilterValues"/> came from cutting a deeper route short
    /// (<see cref="PageRoute.TryMatchPartial"/> - <c>/blog/2026-08</c> read off
    /// <c>/blog/{publishTime:yyyy-MM}/{slug}</c>), rather than from an address the route itself declares
    /// (<see cref="PageRoute.TryMatchExact"/> - <c>/news/2026</c> against
    /// <c>/news/{publishTime?:yyyy}</c>). The two are the same shape to a renderer, but not to a search
    /// engine: a truncated reading is the platform guessing at an address no route ever declared, so it
    /// has no URL of its own to be canonical at, whereas a declared one is a real address like any other
    /// (see <c>HeadMetadataBuilder</c>). Always false when <see cref="FilterValues"/> is empty.
    /// </summary>
    public bool IsTruncated { get; }

    public bool IsMatch => Kind != RouteMatchKind.None;

    /// <summary>
    /// The path named a page and no content: <c>/blog</c>, or <c>/</c>, or a partial match short of a slug
    /// - see <paramref name="filterValues"/>. A front end typically renders a list here, but a page
    /// carrying one single content whose slug is empty also lands here at the bare address - see
    /// <see cref="ContentOfPage"/>.
    /// </summary>
    /// <param name="filterValues">
    /// The values a partial or exact match extracted, when this is one (总体设计 §3.4) - omit for a bare
    /// address, where there is nothing to report.
    /// </param>
    /// <param name="isTruncated">Whether <paramref name="filterValues"/> came from a truncated reading - see <see cref="IsTruncated"/>.</param>
    public static RouteMatch ForPage(
        Page page,
        IReadOnlyDictionary<string, string>? filterValues = null,
        bool isTruncated = false)
    {
        return new RouteMatch(RouteMatchKind.Page, page, filterValues: filterValues, isTruncated: isTruncated);
    }

    /// <summary>
    /// The path named a page whose single content has an empty slug - a home or "about" page. Reported
    /// distinctly from <see cref="ForPage"/> so a front end does not have to guess whether an empty-slug
    /// content exists before deciding between a list and a single page.
    /// </summary>
    public static RouteMatch ForContentOfPage(Page page, Content content, ContentType? contentType)
    {
        return new RouteMatch(RouteMatchKind.ContentOfPage, page, content, contentType);
    }

    /// <summary>The path named a content beneath a page: <c>/blog/my-trip</c>.</summary>
    public static RouteMatch ForContent(Page page, Content content, ContentType? contentType)
    {
        return new RouteMatch(RouteMatchKind.Content, page, content, contentType);
    }

    /// <summary>Nothing matched. The caller's next step is the redirect table, then a real 404.</summary>
    public static RouteMatch None { get; } = new(RouteMatchKind.None);
}

public enum RouteMatchKind : byte
{
    None = 0,

    /// <summary>A page's own route, with no content beneath it at that path.</summary>
    Page = 1,

    /// <summary>A page's own route, resolving to its single empty-slug content.</summary>
    ContentOfPage = 2,

    /// <summary>A content beneath a page, identified by slug.</summary>
    Content = 3
}
