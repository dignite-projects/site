using System;
using System.Threading.Tasks;
using Dignite.Sites.Admin.Permissions;
using Dignite.Sites.ContentTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.Admin.ContentTypes;

[RemoteService(Name = AdminRemoteServiceConsts.RemoteServiceName)]
[Area(AdminRemoteServiceConsts.ModuleName)]
[Authorize(AdminPermissions.ContentTypes.Default)]
[Route("api/site-admin/content-types")]
public class ContentTypeAdminController : AdminController, IContentTypeAdminAppService
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

    [HttpPost]
    [Authorize(AdminPermissions.ContentTypes.Create)]
    public virtual Task<ContentTypeDto> CreateAsync(CreateContentTypeDto input)
    {
        return ContentTypeAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(AdminPermissions.ContentTypes.Update)]
    public virtual Task<ContentTypeDto> UpdateAsync(Guid id, UpdateContentTypeDto input)
    {
        return ContentTypeAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(AdminPermissions.ContentTypes.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return ContentTypeAdminAppService.DeleteAsync(id);
    }
}
