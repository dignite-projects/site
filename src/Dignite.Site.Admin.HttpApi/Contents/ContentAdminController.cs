using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Contents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Contents;

[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Contents.Default)]
[Route("api/site-admin/contents")]
public class ContentAdminController : SiteAdminController, IContentAdminAppService
{
    protected IContentAdminAppService ContentAdminAppService { get; }

    public ContentAdminController(IContentAdminAppService contentAdminAppService)
    {
        ContentAdminAppService = contentAdminAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ContentDto> GetAsync(Guid id)
    {
        return ContentAdminAppService.GetAsync(id);
    }

    /// <summary>
    /// The slug is a query parameter, not a route segment: an empty slug is the legitimate address of a
    /// page's single content (总体设计 §2.4), and an empty route segment cannot be expressed.
    /// </summary>
    [HttpGet]
    [Route("by-slug")]
    public virtual Task<ContentDto?> FindBySlugAsync(Guid pageId, string cultureName, string slug)
    {
        return ContentAdminAppService.FindBySlugAsync(pageId, cultureName, slug);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input)
    {
        return ContentAdminAppService.GetListAsync(input);
    }

    [HttpPost]
    [Authorize(SiteAdminPermissions.Contents.Create)]
    public virtual Task<ContentDto> CreateAsync(CreateContentDto input)
    {
        return ContentAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.Contents.Update)]
    public virtual Task<ContentDto> UpdateAsync(Guid id, UpdateContentDto input)
    {
        return ContentAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.Contents.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return ContentAdminAppService.DeleteAsync(id);
    }
}
