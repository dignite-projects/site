using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.Pages;

namespace Dignite.Sites.Routing;

/// <summary>
/// What a request path resolved to (总体设计 §3.4). The back end answers "which page, which content, what
/// shape"; deciding whether that renders as a single page, a list or a detail view is the front end's
/// call, which is why there is no page-kind here to read.
/// </summary>
public class RouteMatch
{
    private RouteMatch(RouteMatchKind kind, Page? page = null, Content? content = null, ContentType? contentType = null)
    {
        Kind = kind;
        Page = page;
        Content = content;
        ContentType = contentType;
    }

    public RouteMatchKind Kind { get; }

    /// <summary>The matched page. Null only when <see cref="Kind"/> is <see cref="RouteMatchKind.None"/>.</summary>
    public Page? Page { get; }

    /// <summary>The matched content, when the path carried a slug.</summary>
    public Content? Content { get; }

    /// <summary>The matched content's shape - loaded alongside it, since a renderer needs it immediately.</summary>
    public ContentType? ContentType { get; }

    public bool IsMatch => Kind != RouteMatchKind.None;

    /// <summary>
    /// The path named a page and no content: <c>/blog</c>, or <c>/</c>. A front end typically renders a
    /// list here, but a page carrying one single content whose slug is empty also lands here - see
    /// <see cref="ContentOfPage"/>.
    /// </summary>
    public static RouteMatch ForPage(Page page)
    {
        return new RouteMatch(RouteMatchKind.Page, page);
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
