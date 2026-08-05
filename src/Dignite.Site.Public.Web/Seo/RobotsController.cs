using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Caching;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// <c>robots.txt</c> and the optional <c>llms.txt</c>, per domain (总体设计 §5.6, GitHub issue #18).
/// </summary>
public class RobotsController : SiteDocumentController
{
    /// <summary>
    /// An hour. Long enough to be cheap, short enough that flipping an AI crawler switch takes effect
    /// within a working session rather than a working day.
    /// </summary>
    public const int MaxAgeSeconds = 3600;

    protected IRobotsDocumentAppService RobotsDocumentAppService { get; }

    public RobotsController(
        IRobotsDocumentAppService robotsDocumentAppService,
        IDistributedCache<SiteDocumentCacheItem, string> cache)
        : base(cache)
    {
        RobotsDocumentAppService = robotsDocumentAppService;
    }

    // GET and HEAD - see SitemapController for why HEAD has to be listed explicitly.
    [AcceptVerbs("GET", "HEAD", Route = "/robots.txt")]
    public virtual Task<IActionResult> GetRobotsTxtAsync()
    {
        return ServeAsync(
            "robots",
            async () => await RobotsDocumentAppService.GetRobotsTxtAsync(),
            MaxAgeSeconds,
            // Small enough that gzip's framing would eat most of the saving.
            compressible: false);
    }

    [AcceptVerbs("GET", "HEAD", Route = "/llms.txt")]
    public virtual Task<IActionResult> GetLlmsTxtAsync()
    {
        return ServeAsync(
            "llms",
            () => RobotsDocumentAppService.GetLlmsTxtAsync(),
            MaxAgeSeconds,
            compressible: false);
    }
}
