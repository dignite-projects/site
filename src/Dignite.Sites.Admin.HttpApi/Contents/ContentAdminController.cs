using System;
using System.Threading.Tasks;
using Dignite.Sites.Admin.Permissions;
using Dignite.Sites.Contents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.Admin.Contents;

[RemoteService(Name = AdminRemoteServiceConsts.RemoteServiceName)]
[Area(AdminRemoteServiceConsts.ModuleName)]
[Authorize(AdminPermissions.Contents.Default)]
[Route("api/site-admin/contents")]
public class ContentAdminController : AdminController, IContentAdminAppService
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

    [HttpGet]
    public virtual Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input)
    {
        return ContentAdminAppService.GetListAsync(input);
    }

    [HttpPost]
    [Authorize(AdminPermissions.Contents.Create)]
    public virtual Task<ContentDto> CreateAsync(CreateContentDto input)
    {
        return ContentAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(AdminPermissions.Contents.Update)]
    public virtual Task<ContentDto> UpdateAsync(Guid id, UpdateContentDto input)
    {
        return ContentAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(AdminPermissions.Contents.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return ContentAdminAppService.DeleteAsync(id);
    }
}
