using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.ContentTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.ContentTypes;

[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.ContentTypes.Default)]
[Route("api/site-admin/content-types")]
public class ContentTypeAdminController : SiteAdminController, IContentTypeAdminAppService
{
    protected IContentTypeAdminAppService ContentTypeAdminAppService { get; }

    public ContentTypeAdminController(IContentTypeAdminAppService contentTypeAdminAppService)
    {
        ContentTypeAdminAppService = contentTypeAdminAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ContentTypeDto> GetAsync(Guid id)
    {
        return ContentTypeAdminAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("by-page/{pageId}")]
    public virtual Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId)
    {
        return ContentTypeAdminAppService.GetListByPageAsync(pageId);
    }

    [HttpGet]
    public virtual Task<ListResultDto<ContentTypeDto>> GetListAsync()
    {
        return ContentTypeAdminAppService.GetListAsync();
    }

    /// <summary>The name is a query parameter rather than a route segment - see <c>PageAdminController</c>.</summary>
    [HttpGet]
    [Route("by-page/{pageId}/by-name")]
    public virtual Task<ContentTypeDto?> FindByNameAsync(Guid pageId, string name)
    {
        return ContentTypeAdminAppService.FindByNameAsync(pageId, name);
    }

    [HttpPost]
    [Authorize(SiteAdminPermissions.ContentTypes.Create)]
    public virtual Task<ContentTypeDto> CreateAsync(CreateContentTypeDto input)
    {
        return ContentTypeAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.ContentTypes.Update)]
    public virtual Task<ContentTypeDto> UpdateAsync(Guid id, UpdateContentTypeDto input)
    {
        return ContentTypeAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.ContentTypes.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return ContentTypeAdminAppService.DeleteAsync(id);
    }
}
