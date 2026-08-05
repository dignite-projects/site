using Dignite.Sites.Seo;
using Volo.Abp.DependencyInjection;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// The sitemap generator with a shard size small enough that the seeded scenario spills across several
/// files - the sharding behaviour is what needs testing, not the ten thousand URLs it normally takes to
/// trigger it.
/// <para>
/// Exposed only as itself, so the ordinary <see cref="ISitemapDocumentAppService"/> registration is
/// untouched and every other test still sees production's shard size.
/// </para>
/// </summary>
[ExposeServices(typeof(SmallShardSitemapDocumentAppService))]
public class SmallShardSitemapDocumentAppService : SitemapDocumentAppService
{
    public const int ShardSize = 3;

    public SmallShardSitemapDocumentAppService(SitemapEntrySource entrySource, SiteUrlBuilder urlBuilder)
        : base(entrySource, urlBuilder)
    {
    }

    protected override int MaxUrlsPerShard => ShardSize;
}
