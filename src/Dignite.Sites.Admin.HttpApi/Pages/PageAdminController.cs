using System;
using System.Threading.Tasks;
using Dignite.Sites.Admin.Permissions;
using Dignite.Sites.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.Admin.Pages;

[RemoteService(Name = AdminRemoteServiceConsts.RemoteServiceName)]
[Area(AdminRemoteServiceConsts.ModuleName)]
[Authorize(AdminPermissions.Pages.Default)]
[Route("api/site-admin/pages")]
public class PageAdminController : AdminController, IPageAdminAppService
{
    protected IPageAdminAppService PageAdminAppService { get; }

    public PageAdminController(IPageAdminAppService pageAdminAppService)
    {
        PageAdminAppService = pageAdminAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<PageDto> GetAsync(Guid id)
    {
        return PageAdminAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        return PageAdminAppService.GetListAsync(input);
    }

    [HttpPost]
    [Authorize(AdminPermissions.Pages.Create)]
    public virtual Task<PageDto> CreateAsync(CreatePageDto input)
    {
        return PageAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(AdminPermissions.Pages.Update)]
    public virtual Task<PageDto> UpdateAsync(Guid id, UpdatePageDto input)
    {
        return PageAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(AdminPermissions.Pages.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return PageAdminAppService.DeleteAsync(id);
    }
}
