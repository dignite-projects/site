using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// <c>robots.txt</c> and the <c>llms.txt</c> index of its pages, per domain (总体设计 §5.6, GitHub issue #18).
/// </summary>
public interface IRobotsDocumentAppService : IApplicationService
{
    Task<SiteDocument> GetRobotsTxtAsync();

    Task<SiteDocument> GetLlmsTxtAsync();
}
