using Dignite.Site.Contents;
using Dignite.Site.ContentTypes;
using Dignite.Site.Pages;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// The HTTP projection of <c>SiteRouteResolver.ResolveAsync</c>'s <c>RouteMatch</c> (总体设计 §3.4, §7.4) -
/// the <c>resolve-path</c> contract a Tier 1 (out-of-process) front end calls to turn a request path into
/// a page and, if the path carried a slug, the content beneath it.
/// </summary>
public class RouteMatchDto
{
    public bool Matched { get; set; }

    public RouteMatchKindDto Kind { get; set; }

    /// <summary>The matched page. Null only when <see cref="Matched"/> is false.</summary>
    public PageDto? Page { get; set; }

    /// <summary>The matched content, when the path carried a slug or resolved to a page's single empty-slug content.</summary>
    public ContentDto? Content { get; set; }

    /// <summary>The matched content's shape - loaded alongside it, since a renderer needs it immediately.</summary>
    public ContentTypeDto? ContentType { get; set; }
}

/// <summary>
/// Mirrors the domain's <c>RouteMatchKind</c> one-for-one. Kept as a local copy rather than referenced
/// directly - <c>Public.Application.Contracts</c> depends on <c>Domain.Shared</c> only, and this enum
/// lives in the <c>Domain</c> project alongside <c>RouteMatch</c>.
/// </summary>
public enum RouteMatchKindDto : byte
{
    /// <summary>Nothing matched. The caller's next step is the redirect table, then a real 404.</summary>
    None = 0,

    /// <summary>The path named a page and no content: <c>/blog</c>, or <c>/</c>.</summary>
    Page = 1,

    /// <summary>The path named a page whose single content has an empty slug - a home or "about" page.</summary>
    ContentOfPage = 2,

    /// <summary>The path named a content beneath a page: <c>/blog/my-trip</c>.</summary>
    Content = 3
}
