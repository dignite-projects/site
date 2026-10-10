using Dignite.Site.Admin.Files;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// Fills in <see cref="FileDescriptorDto.Url"/>, which the application service leaves empty: over HTTP the
/// admin controller sets it from the request, and a file handed to a model without one cannot be referenced
/// from a content field. Same address as the controller's (<see cref="SiteFileUrl.Build"/>), on the host the
/// MCP request came in on.
/// </summary>
public class SiteMcpFileUrlBuilder : ITransientDependency
{
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public SiteMcpFileUrlBuilder(IHttpContextAccessor httpContextAccessor)
    {
        HttpContextAccessor = httpContextAccessor;
    }

    public virtual FileDescriptorDto WithUrl(FileDescriptorDto file)
    {
        var request = HttpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            file.Url = SiteFileUrl.Build($"{request.Scheme}://{request.Host.Value}", file.ContainerName, file.BlobName, file.TenantId);
        }

        return file;
    }
}
