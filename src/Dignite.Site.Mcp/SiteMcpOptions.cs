namespace Dignite.Site.Mcp;

/// <summary>
/// Options of Site's MCP tools. Configure them in the host with <c>Configure&lt;SiteMcpOptions&gt;</c>.
/// </summary>
public class SiteMcpOptions
{
    /// <summary>
    /// The address a theme download or upload link starts with (<c>site_create_theme_download_url</c>,
    /// <c>site_create_theme_upload_url</c>). <c>null</c>, the default, takes the scheme and host the MCP request came in
    /// on, which is right when the MCP server runs in Site's host behind a proxy whose forwarded headers it trusts. Set it
    /// when a gateway in front does not forward the original host; the gateway then routes
    /// <c>api/site-admin/theme/download/*</c> and <c>api/site-admin/theme/upload/*</c> to Site.
    /// </summary>
    public string? BaseUrl { get; set; }
}
