using System.Threading.Tasks;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Stands in for the real (Mongo/EF-backed) app service so the HTTP round trip can be tested without a
/// database - see <see cref="SeoDocumentHttpServerTestModule"/>.
/// </summary>
public class FakeSitemapDocumentAppService : ISitemapDocumentAppService
{
    public static readonly SiteDocument Index = new("application/xml", "<sitemapindex></sitemapindex>");

    public static readonly SiteDocument FirstShard = new("application/xml", "<urlset></urlset>");

    public const int ExistingShardNumber = 1;

    public Task<SiteDocument> GetIndexAsync()
    {
        return Task.FromResult(Index);
    }

    public Task<SiteDocument?> GetShardAsync(int shardNumber)
    {
        SiteDocument? result = shardNumber == ExistingShardNumber ? FirstShard : null;
        return Task.FromResult(result);
    }
}
