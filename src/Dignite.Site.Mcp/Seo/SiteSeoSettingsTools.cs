using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Seo;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Seo;

/// <summary>The site's SEO settings: the default share image.</summary>
[McpServerToolType]
public class SiteSeoSettingsTools : ITransientDependency
{
    protected ISiteSeoSettingsAdminAppService SeoSettingsAppService { get; }

    public SiteSeoSettingsTools(ISiteSeoSettingsAdminAppService seoSettingsAppService)
    {
        SeoSettingsAppService = seoSettingsAppService;
    }

    [McpServerTool(Name = "site_set_default_share_image", Title = "Set the default share image", Idempotent = true)]
    [Description(
        "Sets the image shown when a page of the site is shared on a social network and the page has no share image of " +
        "its own (its SEO field's ogImage is empty and its template gives none). Give the url of an image in the file " +
        "library (upload it first; 1200 x 630 suits every network, and a library image is cropped to that size) or an " +
        "absolute URL. An empty string removes it. Returns the setting as stored.")]
    [Authorize(SiteAdminPermissions.Seo.Update)]
    public virtual Task<SiteSeoSettingsDto> SetDefaultShareImageAsync(
        [Description("The image's url, from site_upload_file or site_list_files, or an absolute URL; empty to remove it.")]
        string url)
    {
        return SeoSettingsAppService.UpdateAsync(new UpdateSiteSeoSettingsDto { DefaultShareImage = url });
    }
}
