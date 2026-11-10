using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// File paths contain slashes (<c>blog/_sidebar</c>), so they travel as a query parameter, never as a route segment.
/// </summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Themes.Default)]
[Route("api/site-admin/theme")]
public class SiteThemeAdminController : SiteAdminController, ISiteThemeAdminAppService
{
    protected ISiteThemeAdminAppService ThemeAdminAppService { get; }

    public SiteThemeAdminController(ISiteThemeAdminAppService themeAdminAppService)
    {
        ThemeAdminAppService = themeAdminAppService;
    }

    [HttpGet]
    public virtual Task<SiteThemeDto> GetAsync()
    {
        return ThemeAdminAppService.GetAsync();
    }

    [HttpPut]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task<SiteThemeDto> UpdateAsync(UpdateSiteThemeDto input)
    {
        return ThemeAdminAppService.UpdateAsync(input);
    }

    [HttpGet]
    [Route("files")]
    public virtual Task<ListResultDto<SiteThemeFileSummaryDto>> GetFilesAsync()
    {
        return ThemeAdminAppService.GetFilesAsync();
    }

    [HttpGet]
    [Route("files/by-path")]
    public virtual Task<SiteThemeFileDto> GetFileAsync([FromQuery] string path)
    {
        return ThemeAdminAppService.GetFileAsync(path);
    }

    [HttpPut]
    [Route("files")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task<SiteThemeFileDto> SaveFileAsync(SaveSiteThemeFileDto input)
    {
        return ThemeAdminAppService.SaveFileAsync(input);
    }

    [HttpPost]
    [Route("files/rename")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task<SiteThemeRenameResultDto> RenameFileAsync(RenameSiteThemeFileDto input)
    {
        return ThemeAdminAppService.RenameFileAsync(input);
    }

    [HttpDelete]
    [Route("files/by-path")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task DeleteFileAsync([FromQuery] string path)
    {
        return ThemeAdminAppService.DeleteFileAsync(path);
    }

    [HttpGet]
    [Route("files/usages")]
    public virtual Task<SiteThemeFileUsagesDto> GetFileUsagesAsync([FromQuery] string path)
    {
        return ThemeAdminAppService.GetFileUsagesAsync(path);
    }

    [HttpGet]
    [Route("check")]
    public virtual Task<SiteThemeCheckDto> CheckAsync([FromQuery] SiteThemeCheckInput? input = null)
    {
        return ThemeAdminAppService.CheckAsync(input);
    }

    [HttpPost]
    [Route("publish")]
    [Authorize(SiteAdminPermissions.Themes.Publish)]
    public virtual Task<SiteReleaseDto> PublishAsync(PublishSiteThemeDto input)
    {
        return ThemeAdminAppService.PublishAsync(input);
    }

    [HttpGet]
    [Route("releases")]
    public virtual Task<ListResultDto<SiteReleaseDto>> GetReleasesAsync()
    {
        return ThemeAdminAppService.GetReleasesAsync();
    }

    [HttpGet]
    [Route("releases/{id}")]
    public virtual Task<SiteReleaseDetailDto> GetReleaseAsync(Guid id)
    {
        return ThemeAdminAppService.GetReleaseAsync(id);
    }

    [HttpGet]
    [Route("releases/{id}/files/by-path")]
    public virtual Task<SiteReleaseFileContentDto> GetReleaseFileAsync(Guid id, [FromQuery] string path)
    {
        return ThemeAdminAppService.GetReleaseFileAsync(id, path);
    }

    [HttpPost]
    [Route("releases/{id}/set-live")]
    [Authorize(SiteAdminPermissions.Themes.Publish)]
    public virtual Task<SiteReleaseDto> SetLiveReleaseAsync(Guid id)
    {
        return ThemeAdminAppService.SetLiveReleaseAsync(id);
    }

    [HttpPost]
    [Route("releases/{id}/restore")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task RestoreReleaseAsync(Guid id)
    {
        return ThemeAdminAppService.RestoreReleaseAsync(id);
    }

    [HttpGet]
    [Route("preview")]
    public virtual Task<SiteThemePreviewDto> PreviewAsync([FromQuery] string path, [FromQuery] Guid? releaseId = null)
    {
        return ThemeAdminAppService.PreviewAsync(path, releaseId);
    }

    [HttpGet]
    [Route("preview-link")]
    public virtual Task<SiteThemePreviewLinkDto> GetPreviewLinkAsync([FromQuery] string? path = null, [FromQuery] Guid? releaseId = null)
    {
        return ThemeAdminAppService.GetPreviewLinkAsync(path, releaseId);
    }
}
