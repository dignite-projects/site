using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Admin.Seo;

/// <summary>The site's SEO settings, over HTTP and through the <c>site_set_default_share_image</c> MCP tool.</summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Seo.Default)]
[Route("api/site-admin/seo-settings")]
public class SiteSeoSettingsAdminController : SiteAdminController, ISiteSeoSettingsAdminAppService
{
    protected ISiteSeoSettingsAdminAppService SeoSettingsAppService { get; }

    public SiteSeoSettingsAdminController(ISiteSeoSettingsAdminAppService seoSettingsAppService)
    {
        SeoSettingsAppService = seoSettingsAppService;
    }

    [HttpGet]
    public virtual Task<SiteSeoSettingsDto> GetAsync()
    {
        return SeoSettingsAppService.GetAsync();
    }

    [HttpPut]
    [Authorize(SiteAdminPermissions.Seo.Update)]
    public virtual Task<SiteSeoSettingsDto> UpdateAsync(UpdateSiteSeoSettingsDto input)
    {
        return SeoSettingsAppService.UpdateAsync(input);
    }
}
