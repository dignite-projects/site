using System.Globalization;

namespace Dignite.Site.Seo;

/// <summary>
/// The sizes and paths the sitemap is generated at (总体设计 §5.2, GitHub issue #15).
/// </summary>
public static class SitemapConsts
{
    /// <summary>
    /// URLs per shard. The protocol ceiling is 50 000 URLs / 50MB per file; 10 000 is the low end of the
    /// 1–2 万 range §5.2 recommends, which leaves headroom for the day a URL grows extra elements
    /// (image entries, alternates) without any shard approaching either limit. At this size a shard is
    /// roughly 2MB uncompressed, so gzip is a bandwidth saving rather than something correctness depends on.
    /// </summary>
    public const int MaxUrlsPerShard = 10_000;

    /// <summary>The one index per tenant, which <c>robots.txt</c> advertises.</summary>
    public const string IndexPath = "/sitemap.xml";

    // <priority> is advisory and current search engines mostly ignore it - but X.Web.Sitemap's Url.Priority
    // is a non-nullable double that always serializes, so leaving it unset would emit
    // "<priority>0</priority>", the worst signal available. These are the conventional values.
    public const double HomePagePriority = 1.0d;
    public const double PagePriority = 0.8d;
    public const double ContentPriority = 0.5d;

    /// <summary>
    /// Shards are numbered, not named after the page they came from: <c>Page.Name</c> is only checked for
    /// being non-blank and may contain spaces or non-ASCII characters, which is not safe in a path. The
    /// per-page grouping §5.2 asks for is preserved implicitly instead - the entry source emits one page's
    /// URLs contiguously, so a page's URLs stay within as few shards as possible.
    /// </summary>
    public static string BuildShardPath(int shardNumber)
    {
        return string.Create(CultureInfo.InvariantCulture, $"/sitemap-{shardNumber}.xml");
    }
}
