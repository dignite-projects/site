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
[Authorize(AdminPermissions.Fields.Default)]
[Route("api/site-admin/fields")]
public class FieldAdminController : AdminController, IFieldAdminAppService
{
    protected IFieldAdminAppService FieldAdminAppService { get; }

    public FieldAdminController(IFieldAdminAppService fieldAdminAppService)
    {
        FieldAdminAppService = fieldAdminAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<FieldDto> GetAsync(Guid id)
    {
        return FieldAdminAppService.GetAsync(id);
    }

    /// <summary>A query parameter rather than a route segment - see <c>PageAdminController</c>.</summary>
    [HttpGet]
    [Route("by-name")]
    public virtual Task<FieldDto?> FindByNameAsync(string name)
    {
        return FieldAdminAppService.FindByNameAsync(name);
    }

    [HttpGet]
    public virtual Task<ListResultDto<FieldDto>> GetListAsync(GetFieldListInput input)
    {
        return FieldAdminAppService.GetListAsync(input);
    }

    [HttpPost]
    [Authorize(AdminPermissions.Fields.Create)]
    public virtual Task<FieldDto> CreateAsync(CreateFieldDto input)
    {
        return FieldAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(AdminPermissions.Fields.Update)]
    public virtual Task<FieldDto> UpdateAsync(Guid id, UpdateFieldDto input)
    {
        return FieldAdminAppService.UpdateAsync(id, input);
    }

    [HttpPost]
    [Route("{id}/rename")]
    [Authorize(AdminPermissions.Fields.Rename)]
    public virtual Task<FieldDto> RenameAsync(Guid id, RenameFieldDto input)
    {
        return FieldAdminAppService.RenameAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(AdminPermissions.Fields.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return FieldAdminAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("field-types")]
    public virtual Task<ListResultDto<FieldTypeDto>> GetFieldTypesAsync()
    {
        return FieldAdminAppService.GetFieldTypesAsync();
    }
}
