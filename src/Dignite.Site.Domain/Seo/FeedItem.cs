using System;

namespace Dignite.Site.Seo;

/// <summary>One entry in a page's feed (总体设计 §5.9, GitHub issue #31).</summary>
public class FeedItem
{
    public FeedItem(
        string location,
        string title,
        string? summary,
        DateTime publishTime,
        DateTime lastModified)
    {
        Location = location;
        Title = title;
        Summary = summary;
        PublishTime = publishTime;
        LastModified = lastModified;
    }

    /// <summary>
    /// The content's absolute URL. Doubles as the item's identity in every format - a permanent,
    /// per-content unique string is exactly what RSS's <c>guid</c>, Atom's <c>id</c> and JSON Feed's
    /// <c>id</c> each require, and the URL already is one.
    /// </summary>
    public string Location { get; }

    public string Title { get; }

    public string? Summary { get; }

    public DateTime PublishTime { get; }

    /// <summary><see cref="ContentModificationTime.Of"/> - the same value the sitemap reports.</summary>
    public DateTime LastModified { get; }
}
