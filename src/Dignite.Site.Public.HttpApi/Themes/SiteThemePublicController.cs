using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Themes;

[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/theme")]
public class SiteThemePublicController : SitePublicController, ISiteThemePublicAppService
{
    protected ISiteThemePublicAppService ThemePublicAppService { get; }

    public SiteThemePublicController(ISiteThemePublicAppService themePublicAppService)
    {
        ThemePublicAppService = themePublicAppService;
    }

    [HttpGet]
    [Route("live")]
    public virtual Task<SiteLiveThemeDto> GetLiveAsync([FromQuery] Guid? knownReleaseId = null)
    {
        return ThemePublicAppService.GetLiveAsync(knownReleaseId);
    }

    /// <summary>The path is a query parameter, not a route segment - it contains slashes (<c>assets/site.css</c>).</summary>
    [HttpGet]
    [Route("assets")]
    public virtual Task<SiteThemeAssetDto?> FindLiveAssetAsync([FromQuery] string path)
    {
        return ThemePublicAppService.FindLiveAssetAsync(path);
    }
}
