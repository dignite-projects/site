using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dignite.Site.Host.Seo;

/// <summary>
/// Adds the "whatever host this request arrived on" fallback to
/// <see cref="SettingSiteBaseUrlResolver"/>, so a tenant that has not configured a primary domain still
/// gets working absolute URLs.
/// <para>
/// Lives in the Host project, not <c>Dignite.Site.Public.Web</c>: <c>Public.Web</c> is deliberately kept
/// portable (only references <c>Dignite.Site.Public.Application.Contracts</c>, no Domain access at all -
/// see its own csproj), and this override needs both <c>ISiteBaseUrlResolver</c> (Domain) and
/// <c>IHttpContextAccessor</c> (ASP.NET Core), which only a real host - the one thing every deployment of
/// this backend genuinely has - can offer both of at once. <see cref="ISiteBaseUrlResolver"/>'s own doc
/// comment already anticipated this being supplied by "the web layer" rather than living in Domain itself.
/// </para>
/// <para>
/// <b>The setting always wins when it is set</b>, and that is the point rather than a detail. Serving a
/// custom domain and a platform subdomain both at 200 with self-referencing URLs is the classic
/// multi-tenant SEO failure - the same ranking signals split across two addresses (总体设计 §5). Deriving
/// URLs from the request would reintroduce exactly that; the fallback exists only for the unconfigured
/// case, where guessing beats failing.
/// </para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ISiteBaseUrlResolver))]
public class RequestSiteBaseUrlResolver : SettingSiteBaseUrlResolver
{
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public RequestSiteBaseUrlResolver(
        ISettingProvider settingProvider,
        IHttpContextAccessor httpContextAccessor)
        : base(settingProvider)
    {
        HttpContextAccessor = httpContextAccessor;
    }

    public override async Task<string?> GetBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        var configured = await GetConfiguredBaseUrlAsync(cancellationToken);
        if (configured != null)
        {
            return configured;
        }

        var request = HttpContextAccessor.HttpContext?.Request;
        if (request == null || !request.Host.HasValue)
        {
            return null;
        }

        // PathBase, not Path: a module mounted under a path prefix has to keep it, and the request's own
        // path is one specific URL rather than the site root.
        return TryNormalize($"{request.Scheme}://{request.Host}{request.PathBase}", out var normalized)
            ? normalized
            : null;
    }
}
