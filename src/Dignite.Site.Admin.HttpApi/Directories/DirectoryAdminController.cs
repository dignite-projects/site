using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Directories;

[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Route("api/site-admin/directories")]
public class DirectoryAdminController : SiteAdminController, IDirectoryAdminAppService
{
    protected IDirectoryAdminAppService DirectoryAdminAppService { get; }

    public DirectoryAdminController(IDirectoryAdminAppService directoryAdminAppService)
    {
        DirectoryAdminAppService = directoryAdminAppService;
    }

    [HttpGet]
    [Route("{id:guid}")]
    public virtual Task<DirectoryDescriptorDto> GetAsync(Guid id)
    {
        return DirectoryAdminAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<ListResultDto<DirectoryDescriptorInfoDto>> GetListAsync(GetDirectoriesInput input)
    {
        return DirectoryAdminAppService.GetListAsync(input);
    }

    [HttpPost]
    public virtual Task<DirectoryDescriptorDto> CreateAsync(CreateDirectoryInput input)
    {
        return DirectoryAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id:guid}")]
    public virtual Task<DirectoryDescriptorDto> UpdateAsync(Guid id, UpdateDirectoryInput input)
    {
        return DirectoryAdminAppService.UpdateAsync(id, input);
    }

    [HttpPut]
    [Route("{id:guid}/move")]
    public virtual Task<DirectoryDescriptorDto> MoveAsync(Guid id, MoveDirectoryInput input)
    {
        return DirectoryAdminAppService.MoveAsync(id, input);
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return DirectoryAdminAppService.DeleteAsync(id);
    }
}
