using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Schema;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Schema;

/// <summary>
/// The same schema document, also published as an MCP resource (总体设计 §6.2.4).
/// <para>
/// It is not a duplicate surface, it is a shorter path. A client that supports resources has the schema
/// on connect, which drops "post a news item" from two round trips to one; a client that does not calls
/// <c>get_site_schema</c> instead. Both go through the one application service, so there is no second
/// answer to keep in step.
/// </para>
/// </summary>
[McpServerResourceType]
public class SiteSchemaResources : ITransientDependency
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected ISiteSchemaAdminAppService SiteSchemaAppService { get; }

    public SiteSchemaResources(ISiteSchemaAdminAppService siteSchemaAppService)
    {
        SiteSchemaAppService = siteSchemaAppService;
    }

    [McpServerResource(
        UriTemplate = "site://schema",
        Name = "site_schema",
        Title = "The site's content schema",
        MimeType = "application/json")]
    [Description(
        "This site's languages, pages, content types and fields - the target shape for any content " +
        "written here, and the same document get_site_schema returns.")]
    [Authorize(AdminPermissions.Pages.Default)]
    [Authorize(AdminPermissions.ContentTypes.Default)]
    [Authorize(AdminPermissions.Fields.Default)]
    public virtual async Task<string> GetSiteSchemaAsync()
    {
        return JsonSerializer.Serialize(await SiteSchemaAppService.GetAsync(), SerializerOptions);
    }
}
