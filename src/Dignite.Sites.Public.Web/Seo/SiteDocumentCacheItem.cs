using System;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// A generated document held in the distributed cache.
/// <para>
/// Cached rather than regenerated per request because every one of these documents costs a full walk of
/// the tenant's routing table, and crawlers fetch them far more often than the content behind them
/// changes. The expiry is a plain absolute window rather than an invalidation scheme: publishing is not
/// the only thing that changes these documents (a settings change does too), and a short window is
/// honest about that where a hand-maintained invalidation list would silently go stale.
/// </para>
/// <para>
/// ABP's <c>IDistributedCache&lt;T&gt;</c> prefixes every key with the current tenant, which is what
/// makes "one tenant's rules must never appear on another's domain" true by construction rather than by
/// remembering to filter.
/// </para>
/// </summary>
[Serializable]
public class SiteDocumentCacheItem
{
    /// <summary>How long a generated document is served before it is rebuilt.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

    public SiteDocumentCacheItem()
    {
        ContentType = string.Empty;
        Content = string.Empty;
    }

    public SiteDocumentCacheItem(string contentType, string content, DateTime? lastModified, bool found)
    {
        ContentType = contentType;
        Content = content;
        LastModified = lastModified;
        Found = found;
    }

    /// <summary>
    /// Whether the document exists at all. A miss is cached too - otherwise every request for a feed on a
    /// page that has none would walk the routing table again.
    /// </summary>
    public bool Found { get; set; }

    public string ContentType { get; set; }

    public string Content { get; set; }

    public DateTime? LastModified { get; set; }
}
