using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Every route with an id segment constrains it to a Guid (<c>{id:guid}</c>), so a literal segment next to it
/// - <c>by-name</c>, <c>unmatched-pages</c> - can never be read as an id, whatever order the routes are
/// matched in.
/// </summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Templates.Default)]
[Route("api/site-admin/templates")]
public class SiteTemplateAdminController : SiteAdminController, ISiteTemplateAdminAppService
{
    protected ISiteTemplateAdminAppService SiteTemplateAdminAppService { get; }

    public SiteTemplateAdminController(ISiteTemplateAdminAppService siteTemplateAdminAppService)
    {
        SiteTemplateAdminAppService = siteTemplateAdminAppService;
    }

    [HttpGet]
    [Route("{id:guid}")]
    public virtual Task<SiteTemplateDto> GetAsync(Guid id)
    {
        return SiteTemplateAdminAppService.GetAsync(id);
    }

    /// <summary>The name is a query parameter, not a route segment - it contains slashes (<c>Blog/Article</c>).</summary>
    [HttpGet]
    [Route("by-name")]
    public virtual Task<SiteTemplateDto?> FindByNameAsync(string name)
    {
        return SiteTemplateAdminAppService.FindByNameAsync(name);
    }

    [HttpGet]
    public virtual Task<ListResultDto<SiteTemplateDto>> GetListAsync(GetSiteTemplateListInput input)
    {
        return SiteTemplateAdminAppService.GetListAsync(input);
    }

    [HttpPost]
    [Authorize(SiteAdminPermissions.Templates.Create)]
    public virtual Task<SiteTemplateDto> CreateAsync(CreateSiteTemplateDto input)
    {
        return SiteTemplateAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id:guid}")]
    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual Task<SiteTemplateDto> UpdateAsync(Guid id, UpdateSiteTemplateDto input)
    {
        return SiteTemplateAdminAppService.UpdateAsync(id, input);
    }

    [HttpPost]
    [Route("{id:guid}/publish/{versionNumber}")]
    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual Task<SiteTemplateDto> PublishAsync(Guid id, int versionNumber)
    {
        return SiteTemplateAdminAppService.PublishAsync(id, versionNumber);
    }

    [HttpGet]
    [Route("{id:guid}/versions")]
    public virtual Task<ListResultDto<SiteTemplateVersionDto>> GetVersionsAsync(Guid id)
    {
        return SiteTemplateAdminAppService.GetVersionsAsync(id);
    }

    [HttpGet]
    [Route("{id:guid}/versions/{versionNumber}")]
    public virtual Task<SiteTemplateVersionDto> GetVersionAsync(Guid id, int versionNumber)
    {
        return SiteTemplateAdminAppService.GetVersionAsync(id, versionNumber);
    }

    /// <summary>The path is a query parameter, not a route segment - it contains slashes (<c>/ja/blog/hello</c>).</summary>
    [HttpGet]
    [Route("{id:guid}/versions/{versionNumber}/preview")]
    public virtual Task<SiteTemplatePreviewDto> PreviewAsync(Guid id, int versionNumber, string path)
    {
        return SiteTemplateAdminAppService.PreviewAsync(id, versionNumber, path);
    }

    [HttpGet]
    [Route("{id:guid}/usage")]
    public virtual Task<SiteTemplateUsageDto> GetUsageAsync(Guid id)
    {
        return SiteTemplateAdminAppService.GetUsageAsync(id);
    }

    [HttpGet]
    [Route("unmatched-pages")]
    public virtual Task<ListResultDto<SiteTemplateUnmatchedPageDto>> GetUnmatchedPagesAsync()
    {
        return SiteTemplateAdminAppService.GetUnmatchedPagesAsync();
    }

    /// <summary>The path is a query parameter, not a route segment - it contains slashes (<c>/ja/blog/hello</c>).</summary>
    [HttpGet]
    [Route("{id:guid}/versions/{versionNumber}/preview-link")]
    public virtual Task<SiteTemplatePreviewLinkDto> GetPreviewLinkAsync(Guid id, int versionNumber, string path)
    {
        return SiteTemplateAdminAppService.GetPreviewLinkAsync(id, versionNumber, path);
    }

    [HttpDelete]
    [Route("{id:guid}")]
    [Authorize(SiteAdminPermissions.Templates.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return SiteTemplateAdminAppService.DeleteAsync(id);
    }
}
