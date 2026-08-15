using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Routing;

[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/resolve-path")]
public class RoutingPublicController : SitePublicController, IRoutingPublicAppService
{
    protected IRoutingPublicAppService RoutingPublicAppService { get; }

    public RoutingPublicController(IRoutingPublicAppService routingPublicAppService)
    {
        RoutingPublicAppService = routingPublicAppService;
    }

    [HttpGet]
    public virtual Task<RouteMatchDto> ResolveAsync([FromQuery] ResolvePathInput input)
    {
        return RoutingPublicAppService.ResolveAsync(input);
    }
}
