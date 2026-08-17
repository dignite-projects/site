using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Seo;

[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/robots")]
public class RobotsDocumentPublicController : SitePublicController, IRobotsDocumentAppService
{
    protected IRobotsDocumentAppService RobotsDocumentAppService { get; }

    public RobotsDocumentPublicController(IRobotsDocumentAppService robotsDocumentAppService)
    {
        RobotsDocumentAppService = robotsDocumentAppService;
    }

    [HttpGet]
    public virtual Task<SiteDocument> GetRobotsTxtAsync()
    {
        return RobotsDocumentAppService.GetRobotsTxtAsync();
    }

    [HttpGet]
    [Route("llms-txt")]
    public virtual Task<SiteDocument?> GetLlmsTxtAsync()
    {
        return RobotsDocumentAppService.GetLlmsTxtAsync();
    }
}
