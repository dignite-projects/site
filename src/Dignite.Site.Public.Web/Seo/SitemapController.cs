using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Caching;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The sitemap index and its shards, at the addresses crawlers and <c>robots.txt</c> expect
/// (总体设计 §5.2, GitHub issue #15).
/// </summary>
public class SitemapController : SiteDocumentController
{
    /// <summary>An hour. The document changes when content is published, which is not a per-minute event.</summary>
    public const int MaxAgeSeconds = 3600;

    protected ISitemapDocumentAppService SitemapDocumentAppService { get; }

    public SitemapController(
        ISitemapDocumentAppService sitemapDocumentAppService,
        IDistributedCache<SiteDocumentCacheItem, string> cache)
        : base(cache)
    {
        SitemapDocumentAppService = sitemapDocumentAppService;
    }

    // GET and HEAD: crawlers and monitors routinely probe these documents with HEAD, and ASP.NET Core does
    // not fall back from a GET-only route, so a HEAD would otherwise 404 an address that plainly exists.
    [AcceptVerbs("GET", "HEAD", Route = "/sitemap.xml")]
    public virtual Task<IActionResult> GetIndexAsync()
    {
        return ServeAsync(
            "sitemap:index",
            async () => await SitemapDocumentAppService.GetIndexAsync(),
            MaxAgeSeconds,
            compressible: true);
    }

    [AcceptVerbs("GET", "HEAD", Route = "/sitemap-{shardNumber:int:min(1)}.xml")]
    public virtual Task<IActionResult> GetShardAsync(int shardNumber)
    {
        return ServeAsync(
            "sitemap:" + shardNumber.ToString(CultureInfo.InvariantCulture),
            () => SitemapDocumentAppService.GetShardAsync(shardNumber),
            MaxAgeSeconds,
            compressible: true);
    }
}
