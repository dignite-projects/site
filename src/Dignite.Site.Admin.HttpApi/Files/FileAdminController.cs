using System;
using System.Threading.Tasks;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// The file library's admin API. Every <see cref="FileDescriptorDto.Url"/> it returns is the public read
/// endpoint's relative address (<see cref="SiteFileUrl.Build"/>); a client that shows the file from another
/// origin - the admin UI - puts its own API host in front of it.
/// </summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Route("api/site-admin/files")]
public class FileAdminController : SiteAdminController, IFileAdminAppService
{
    protected IFileAdminAppService FileAdminAppService { get; }

    public FileAdminController(IFileAdminAppService fileAdminAppService)
    {
        FileAdminAppService = fileAdminAppService;
    }

    [HttpGet]
    [Route("{id:guid}")]
    public virtual Task<FileDescriptorDto> GetAsync(Guid id)
    {
        return FileAdminAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<FileDescriptorDto>> GetListAsync(GetFilesInput input)
    {
        return FileAdminAppService.GetListAsync(input);
    }

    /// <summary>
    /// <c>multipart/form-data</c>: the file under <c>file</c>; <c>containerName</c> and <c>directoryId</c> as
    /// query parameters, so <see cref="FileUploadSizeLimitFilter"/> knows the container before the body is read.
    /// </summary>
    [HttpPost]
    [TypeFilter(typeof(FileUploadSizeLimitFilter))]
    public virtual Task<FileDescriptorDto> CreateAsync(CreateFileInput input)
    {
        return FileAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id:guid}")]
    public virtual Task<FileDescriptorDto> UpdateAsync(Guid id, UpdateFileInput input)
    {
        return FileAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return FileAdminAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("containers/{containerName}")]
    public virtual Task<FileContainerConfigurationDto> GetContainerConfigurationAsync(string containerName)
    {
        return FileAdminAppService.GetContainerConfigurationAsync(containerName);
    }
}
