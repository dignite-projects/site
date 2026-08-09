using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Routing;

/// <summary>
/// Answers "what does this URL point at" (总体设计 §3.4, §7.4).
/// <para>
/// The one tool that calls the Public side rather than the Admin side, so it resolves against
/// <b>published content only</b> - which is what makes it useful for checking what a visitor actually
/// sees, and what makes it unable to find a draft. It therefore carries no permission of its own, in
/// keeping with the rest of the Public surface.
/// </para>
/// </summary>
[McpServerToolType]
public class RoutingTools : ITransientDependency
{
    protected IRoutingPublicAppService RoutingAppService { get; }

    public RoutingTools(IRoutingPublicAppService routingAppService)
    {
        RoutingAppService = routingAppService;
    }

    [McpServerTool(Name = "resolve_path", Title = "Resolve a URL path", ReadOnly = true)]
    [Description(
        "Resolves a request path to the page and, if the path names one, the content beneath it - the " +
        "same page/content matching a visitor's request goes through. It does not consult the redirect " +
        "table, so a path that comes back unmatched may still work for a visitor if a redirect covers " +
        "it. Published content only: a draft resolves to nothing, so a match here means something is " +
        "actually live at that URL through routing. A path that names some but not all of a page's " +
        "placeholders short of its slug (e.g. '/blog/2026-08' against '/blog/{publishTime:yyyy-MM}/{slug}') " +
        "resolves to that page with filterValues populated instead of a specific content.")]
    public virtual Task<RouteMatchDto> ResolvePathAsync(
        [Description("The path to resolve, e.g. '/blog/my-trip' or '/'.")]
        string path,
        [Description("Language tag to resolve in, from the schema's enabledLanguages.")]
        string cultureName)
    {
        return RoutingAppService.ResolveAsync(new ResolvePathInput
        {
            Path = path,
            CultureName = cultureName
        });
    }
}
