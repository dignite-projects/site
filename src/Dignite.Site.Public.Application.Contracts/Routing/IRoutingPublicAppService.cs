using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// The <c>resolve-path</c> contract (总体设计 §7.4). Always resolves against published content only -
/// unlike <c>SiteRouteResolver.ResolveAsync</c> itself, there is no <c>includeUnpublished</c> override on
/// the public surface, matching every other Public service's "published content only" rule.
/// </summary>
public interface IRoutingPublicAppService : IApplicationService
{
    Task<RouteMatchDto> ResolveAsync(ResolvePathInput input);
}
