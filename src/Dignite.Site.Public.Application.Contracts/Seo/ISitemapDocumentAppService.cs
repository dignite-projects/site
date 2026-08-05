using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The XML sitemap, as one index plus numbered shards (总体设计 §5.2, GitHub issue #15).
/// <para>
/// One index per tenant, and both documents are built from the current tenant's routing table alone -
/// tenants cannot see each other's URLs because there is no cross-tenant document to filter in the first
/// place.
/// </para>
/// </summary>
public interface ISitemapDocumentAppService : IApplicationService
{
    /// <summary>
    /// The sitemap index. Always emitted, even when a single shard would fit the whole site: it is the
    /// stable address <c>robots.txt</c> advertises, so it must not appear and disappear with the site's
    /// size.
    /// </summary>
    Task<SiteDocument> GetIndexAsync();

    /// <summary>
    /// One shard, numbered from 1, or <see langword="null"/> when there is no such shard.
    /// </summary>
    Task<SiteDocument?> GetShardAsync(int shardNumber);
}
