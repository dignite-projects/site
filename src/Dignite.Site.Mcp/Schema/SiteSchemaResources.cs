using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Abp.AspNetCore.Mcp;
using Dignite.Site.Admin.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Schema;

/// <summary>
/// The same schema document, also published as an MCP resource (总体设计 §6.2.4).
/// <para>
/// It is not a duplicate surface, it is a shorter path. A client that supports resources has the schema
/// on connect, which drops "post a news item" from two round trips to one; a client that does not calls
/// <c>site_get_schema</c> instead. Both go through the one application service, so there is no second
/// answer to keep in step.
/// </para>
/// </summary>
[McpServerResourceType]
public class SiteSchemaResources : ITransientDependency
{
    protected ISiteSchemaAdminAppService SiteSchemaAppService { get; }

    /// <summary>
    /// The options every other result of this server is written with: this resource returns text it serializes itself, so
    /// it takes the same encoder (non-ASCII text and markup as written) rather than a set of its own.
    /// </summary>
    protected JsonSerializerOptions SerializerOptions { get; }

    public SiteSchemaResources(ISiteSchemaAdminAppService siteSchemaAppService, IOptions<AbpMcpServerOptions> mcpOptions)
    {
        SiteSchemaAppService = siteSchemaAppService;
        SerializerOptions = mcpOptions.Value.SerializerOptions;
    }

    [McpServerResource(
        UriTemplate = "site://schema",
        Name = "site_schema",
        Title = "The site's content schema",
        MimeType = "application/json")]
    [Description(
        "This site's languages, pages, content types and fields - the target shape for any content " +
        "written here, and the same document site_get_schema returns.")]
    [Authorize(SiteAdminPermissions.Pages.Default)]
    [Authorize(SiteAdminPermissions.ContentTypes.Default)]
    [Authorize(SiteAdminPermissions.Fields.Default)]
    public virtual async Task<string> GetSiteSchemaAsync()
    {
        return JsonSerializer.Serialize(await SiteSchemaAppService.GetAsync(), SerializerOptions);
    }
}
