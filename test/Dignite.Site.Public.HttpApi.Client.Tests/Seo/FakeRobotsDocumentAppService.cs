using System;
using System.Threading.Tasks;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Stands in for the real (Mongo/EF-backed) app service so the HTTP round trip can be tested without a
/// database - see <see cref="SeoDocumentHttpServerTestModule"/>.
/// </summary>
public class FakeRobotsDocumentAppService : IRobotsDocumentAppService
{
    public static readonly SiteDocument RobotsTxt = new(
        "text/plain",
        "User-agent: *\nAllow: /\n",
        new DateTime(2026, 8, 10, 12, 30, 0, DateTimeKind.Utc));

    public Task<SiteDocument> GetRobotsTxtAsync()
    {
        return Task.FromResult(RobotsTxt);
    }

    /// <summary>Always null, the same as a tenant that never opted into publishing this document.</summary>
    public Task<SiteDocument?> GetLlmsTxtAsync()
    {
        return Task.FromResult<SiteDocument?>(null);
    }
}
