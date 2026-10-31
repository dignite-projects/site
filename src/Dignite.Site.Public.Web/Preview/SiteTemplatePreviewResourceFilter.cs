using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// Exchanges the token of a preview link for the preview cookie - on <see cref="Routing.SiteRenderController"/>,
/// which every link the administration side issues opens. A <c>GET</c> or <c>HEAD</c> carrying a non-empty
/// <see cref="SiteTemplatePreviewConsts.QueryKey"/> is always answered with a redirect to the same address without
/// it, so the token never lingers in the address bar, the history or a <c>Referer</c>:
/// <list type="bullet">
/// <item>a token that verifies and was issued for the current tenant first sets the cookie, holding a fresh ticket
/// that lasts <see cref="SiteTemplatePreviewOptions.CookieLifetime"/>;</item>
/// <item>any other token - expired, tampered, another tenant's - sets nothing, and the visitor sees the published
/// site, with no hint of why.</item>
/// </list>
/// A resource filter so it runs before model binding and the action, and after tenant resolution (MVC runs after
/// <c>UseMultiTenancy()</c> by construction). Applied with <c>[ServiceFilter]</c> on the controller, which resolves
/// it from DI - registered by <see cref="ITransientDependency"/>.
/// </summary>
public class SiteTemplatePreviewResourceFilter : IAsyncResourceFilter, ITransientDependency
{
    protected SiteTemplatePreviewTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IOptions<SiteTemplatePreviewOptions> Options { get; }

    public SiteTemplatePreviewResourceFilter(
        SiteTemplatePreviewTokenProtector protector,
        ICurrentTenant currentTenant,
        IOptions<SiteTemplatePreviewOptions> options)
    {
        Protector = protector;
        CurrentTenant = currentTenant;
        Options = options;
    }

    public virtual async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            || !request.Query.TryGetValue(SiteTemplatePreviewConsts.QueryKey, out var token)
            || string.IsNullOrEmpty(token.ToString()))
        {
            await next();
            return;
        }

        var response = context.HttpContext.Response;

        if (Protector.TryUnprotect(token.ToString(), out var ticket) && ticket.TenantId == CurrentTenant.Id)
        {
            // A fresh value rather than the link's token: the cookie outlives the link.
            var cookieValue = Protector.Protect(ticket, Options.Value.CookieLifetime, out var expiresAt);
            response.Cookies.Append(
                SiteTemplatePreviewConsts.CookieName,
                cookieValue,
                SiteTemplatePreviewHttp.CreateCookieOptions(request, expiresAt));
        }

        SiteTemplatePreviewHttp.ApplyHeaders(response);
        context.Result = new RedirectResult(GetAddressWithoutToken(request), permanent: false);
    }

    /// <summary>
    /// The request's own address - base path, path and query - without <see cref="SiteTemplatePreviewConsts.QueryKey"/>.
    /// Relative, so the redirect stays on this origin; a path that starts with more than one slash or a backslash is
    /// reduced to one slash, since <c>//evil.example</c> would be read as another host.
    /// </summary>
    protected virtual string GetAddressWithoutToken(HttpRequest request)
    {
        var path = (request.PathBase + request.Path).ToUriComponent();
        if (path.Length == 0)
        {
            path = "/";
        }
        else if (path.Length > 1 && (path[1] == '/' || path[1] == '\\'))
        {
            path = "/" + path.TrimStart('/', '\\');
        }

        // Query keys compare ignoring case, as IQueryCollection looked the token up.
        var query = new QueryBuilder(request.Query
            .Where(pair => !string.Equals(pair.Key, SiteTemplatePreviewConsts.QueryKey, StringComparison.OrdinalIgnoreCase)));

        return path + query.ToQueryString().ToUriComponent();
    }
}
