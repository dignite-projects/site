using System.Threading.Tasks;
using Asp.Versioning;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// <see cref="ISiteThemePreviewIntegrationService"/> over HTTP, under <c>/integration-api/site-public/theme-preview</c>:
/// hidden unless the application sets <c>ExposeIntegrationServices</c>. POST, so the token travels in the body and not in
/// an address a log keeps.
/// </summary>
[IntegrationService]
[RemoteService(Name = SitePublicRemoteServiceConsts.IntegrationRemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[ControllerName("SiteThemePreviewIntegration")]
[Route("integration-api/site-public/theme-preview")]
public class SiteThemePreviewIntegrationController : SitePublicController, ISiteThemePreviewIntegrationService
{
    protected ISiteThemePreviewIntegrationService IntegrationService { get; }

    public SiteThemePreviewIntegrationController(ISiteThemePreviewIntegrationService integrationService)
    {
        IntegrationService = integrationService;
    }

    [HttpPost]
    [Route("snapshot")]
    public virtual Task<SiteThemeSnapshotDto?> FindSnapshotAsync(SiteThemePreviewSnapshotInput input)
    {
        return IntegrationService.FindSnapshotAsync(input);
    }

    [HttpPost]
    [Route("asset")]
    public virtual Task<SiteThemeAssetDto?> FindAssetAsync(SiteThemePreviewAssetInput input)
    {
        return IntegrationService.FindAssetAsync(input);
    }
}
