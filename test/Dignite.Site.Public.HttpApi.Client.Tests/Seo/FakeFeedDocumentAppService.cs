using System.Threading.Tasks;
using Dignite.Site.Seo;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Stands in for the real (Mongo/EF-backed) app service so the HTTP round trip can be tested without a
/// database - see <see cref="SeoDocumentHttpServerTestModule"/>.
/// </summary>
public class FakeFeedDocumentAppService : IFeedDocumentAppService
{
    public static readonly SiteDocument BlogFeed = new(
        "application/rss+xml", "<rss version=\"2.0\"><channel></channel></rss>");

    public const string KnownPagePath = "/blog";

    public const string KnownFeedPath = "blog/feed.xml";

    public Task<SiteDocument?> GetAsync(string pagePath, string cultureName, SiteFeedFormat format)
    {
        SiteDocument? result = pagePath == KnownPagePath ? BlogFeed : null;
        return Task.FromResult(result);
    }

    public Task<SiteDocument?> ResolveAsync(string feedPath)
    {
        SiteDocument? result = feedPath == KnownFeedPath ? BlogFeed : null;
        return Task.FromResult(result);
    }
}
