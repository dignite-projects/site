using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Templates;

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
    [Route("{id}")]
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
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual Task<SiteTemplateDto> UpdateAsync(Guid id, UpdateSiteTemplateDto input)
    {
        return SiteTemplateAdminAppService.UpdateAsync(id, input);
    }

    [HttpPost]
    [Route("{id}/publish/{versionNumber}")]
    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual Task<SiteTemplateDto> PublishAsync(Guid id, int versionNumber)
    {
        return SiteTemplateAdminAppService.PublishAsync(id, versionNumber);
    }

    [HttpGet]
    [Route("{id}/versions")]
    public virtual Task<ListResultDto<SiteTemplateVersionDto>> GetVersionsAsync(Guid id)
    {
        return SiteTemplateAdminAppService.GetVersionsAsync(id);
    }

    [HttpGet]
    [Route("{id}/versions/{versionNumber}")]
    public virtual Task<SiteTemplateVersionDto> GetVersionAsync(Guid id, int versionNumber)
    {
        return SiteTemplateAdminAppService.GetVersionAsync(id, versionNumber);
    }

    /// <summary>The path is a query parameter, not a route segment - it contains slashes (<c>/ja/blog/hello</c>).</summary>
    [HttpGet]
    [Route("{id}/versions/{versionNumber}/preview")]
    public virtual Task<SiteTemplatePreviewDto> PreviewAsync(Guid id, int versionNumber, string path)
    {
        return SiteTemplateAdminAppService.PreviewAsync(id, versionNumber, path);
    }

    /// <summary>
    /// The same preview as the page itself rather than JSON, for a browser signed in to the administration
    /// side - the admin UI opens it in a frame or a new tab. Not part of <see cref="ISiteTemplateAdminAppService"/>
    /// and hidden from the API explorer (<c>IgnoreApi</c>), so it stays out of the API definition and every proxy
    /// generator - the C# client proxy and the Angular one - ignores it.
    /// </summary>
    [HttpGet]
    [Route("{id}/versions/{versionNumber}/preview.html")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public virtual async Task<IActionResult> PreviewHtmlAsync(Guid id, int versionNumber, string path)
    {
        var result = await SiteTemplateAdminAppService.PreviewAsync(id, versionNumber, path);

        // A preview is never indexed (总体设计 §5.3: drafts and previews are noindex). The page's own robots meta
        // is the live page's, so the header says it for the preview.
        Response.Headers["X-Robots-Tag"] = "noindex";
        return Content(result.Html, "text/html; charset=utf-8");
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.Templates.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return SiteTemplateAdminAppService.DeleteAsync(id);
    }
}
