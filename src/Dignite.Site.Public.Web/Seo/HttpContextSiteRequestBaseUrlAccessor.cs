using Dignite.Site.Seo;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Seo;

/// <summary>Reads <see cref="ISiteRequestBaseUrlAccessor"/>'s answer from the current HTTP request.</summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ISiteRequestBaseUrlAccessor))]
public class HttpContextSiteRequestBaseUrlAccessor : ISiteRequestBaseUrlAccessor, ISingletonDependency
{
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public HttpContextSiteRequestBaseUrlAccessor(IHttpContextAccessor httpContextAccessor)
    {
        HttpContextAccessor = httpContextAccessor;
    }

    public virtual string? GetBaseUrl()
    {
        var request = HttpContextAccessor.HttpContext?.Request;

        // PathBase, not Path: a site mounted under a path keeps it, and the request's own path is one URL
        // rather than the site root.
        return request is { Host.HasValue: true } ? $"{request.Scheme}://{request.Host}{request.PathBase}" : null;
    }
}
