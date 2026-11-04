using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Admin.Languages;

/// <summary>The languages the site serves, over HTTP and through the <c>site_set_languages</c> MCP tool.</summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Languages.Default)]
[Route("api/site-admin/languages")]
public class SiteLanguageAdminController : SiteAdminController, ISiteLanguageAdminAppService
{
    protected ISiteLanguageAdminAppService SiteLanguageAdminAppService { get; }

    public SiteLanguageAdminController(ISiteLanguageAdminAppService siteLanguageAdminAppService)
    {
        SiteLanguageAdminAppService = siteLanguageAdminAppService;
    }

    [HttpGet]
    public virtual Task<SiteLanguagesDto> GetAsync()
    {
        return SiteLanguageAdminAppService.GetAsync();
    }

    [HttpPut]
    [Authorize(SiteAdminPermissions.Languages.Update)]
    public virtual Task<SiteLanguagesDto> UpdateAsync(UpdateSiteLanguagesDto input)
    {
        return SiteLanguageAdminAppService.UpdateAsync(input);
    }
}
