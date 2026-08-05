namespace Dignite.Sites.Seo;

/// <summary>The three syndication formats a page's feed is offered in (总体设计 §5.9, GitHub issue #31).</summary>
public enum SiteFeedFormat : byte
{
    /// <summary>RSS 2.0, served at <c>feed.xml</c>.</summary>
    Rss = 0,

    /// <summary>Atom 1.0, served at <c>atom.xml</c>.</summary>
    Atom = 1,

    /// <summary>JSON Feed 1.1, served at <c>feed.json</c>.</summary>
    Json = 2
}
