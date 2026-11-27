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

    public static readonly SiteDocument LlmsTxt = new(
        "text/markdown",
        "# Example\n\n## Pages\n- [Example](https://www.example.com/)\n");

    public Task<SiteDocument> GetLlmsTxtAsync()
    {
        return Task.FromResult(LlmsTxt);
    }
}
