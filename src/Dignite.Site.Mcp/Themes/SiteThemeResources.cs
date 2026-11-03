using System.ComponentModel;
using Dignite.Site.Admin.Permissions;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Themes;

/// <summary>
/// The theme guide, also published as an MCP resource: the same document <c>site_get_theme_guide</c> returns, as
/// <see cref="Schema.SiteSchemaResources"/> is for <c>site_get_schema</c>. A client that supports resources has it on
/// connect; one that does not calls the tool. Both read <see cref="SiteThemeGuide"/>, so there is one text.
/// </summary>
[McpServerResourceType]
public class SiteThemeResources : ITransientDependency
{
    [McpServerResource(
        UriTemplate = "site://theme-guide",
        Name = "site_theme_guide",
        Title = "How to write the site's theme",
        MimeType = "text/markdown")]
    [Description(
        "How to write a Scriban theme for this site: the variables a template sees, every function it can call, " +
        "layouts, partials, static files and the limits of the sandbox - the same document site_get_theme_guide returns.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual string GetThemeGuide()
    {
        return SiteThemeGuide.Text;
    }
}
