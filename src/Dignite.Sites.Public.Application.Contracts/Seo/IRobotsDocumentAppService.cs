using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// <c>robots.txt</c> and the optional <c>llms.txt</c>, per domain (总体设计 §5.6, GitHub issue #18).
/// </summary>
public interface IRobotsDocumentAppService : IApplicationService
{
    Task<SiteDocument> GetRobotsTxtAsync();

    /// <summary>
    /// <see langword="null"/> unless the tenant opted in - a site that does not publish this file should
    /// 404 rather than serve an empty one.
    /// </summary>
    Task<SiteDocument?> GetLlmsTxtAsync();
}
