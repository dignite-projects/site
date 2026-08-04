using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Sites.Public.Routing;

[RemoteService(Name = PublicRemoteServiceConsts.RemoteServiceName)]
[Area(PublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/resolve-path")]
public class RoutingPublicController : PublicController, IRoutingPublicAppService
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
