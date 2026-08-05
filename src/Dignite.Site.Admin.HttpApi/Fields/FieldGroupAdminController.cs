using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Fields;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Fields;

[RemoteService(Name = AdminRemoteServiceConsts.RemoteServiceName)]
[Area(AdminRemoteServiceConsts.ModuleName)]
[Authorize(AdminPermissions.FieldGroups.Default)]
[Route("api/site-admin/field-groups")]
public class FieldGroupAdminController : AdminController, IFieldGroupAdminAppService
{
    protected IFieldGroupAdminAppService FieldGroupAdminAppService { get; }

    public FieldGroupAdminController(IFieldGroupAdminAppService fieldGroupAdminAppService)
    {
        FieldGroupAdminAppService = fieldGroupAdminAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<FieldGroupDto> GetAsync(Guid id)
    {
        return FieldGroupAdminAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<ListResultDto<FieldGroupDto>> GetListAsync()
    {
        return FieldGroupAdminAppService.GetListAsync();
    }

    [HttpPost]
    [Authorize(AdminPermissions.FieldGroups.Create)]
    public virtual Task<FieldGroupDto> CreateAsync(CreateFieldGroupDto input)
    {
        return FieldGroupAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(AdminPermissions.FieldGroups.Update)]
    public virtual Task<FieldGroupDto> UpdateAsync(Guid id, UpdateFieldGroupDto input)
    {
        return FieldGroupAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(AdminPermissions.FieldGroups.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return FieldGroupAdminAppService.DeleteAsync(id);
    }
}
