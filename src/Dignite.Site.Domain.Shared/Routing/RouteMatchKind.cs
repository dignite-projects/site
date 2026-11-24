namespace Dignite.Site.Routing;

/// <summary>What a request path resolved to - the shape a front end renders (总体设计 §3.4).</summary>
public enum RouteMatchKind : byte
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
