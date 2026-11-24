using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp;

/// <summary>
/// Makes the links the tools hand out (uploads, theme downloads and uploads) absolute: on <see cref="SiteMcpOptions.BaseUrl"/>,
/// else on the scheme and host this MCP request came in on. A host behind a gateway routes those paths through.
/// </summary>
public class SiteMcpLinks : ITransientDependency
{
    protected SiteMcpOptions Options { get; }

    protected IHttpContextAccessor HttpContextAccessor { get; }

    public SiteMcpLinks(IOptions<SiteMcpOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        Options = options.Value;
        HttpContextAccessor = httpContextAccessor;
    }

    /// <param name="path">A path of this application, with or without its leading <c>/</c>.</param>
    public virtual string ToAbsolute(string path)
    {
        return GetBaseUrl().TrimEnd('/') + "/" + path.TrimStart('/');
    }

    protected virtual string GetBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(Options.BaseUrl))
        {
            return Options.BaseUrl;
        }

        var request = HttpContextAccessor.HttpContext?.Request
                      ?? throw new InvalidOperationException(
                          $"The address of a link is unknown: {nameof(SiteMcpOptions)}.{nameof(SiteMcpOptions.BaseUrl)} is not set and there is no request to take it from.");

        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }
}
