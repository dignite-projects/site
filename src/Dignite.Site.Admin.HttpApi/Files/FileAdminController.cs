using System;
using System.Threading.Tasks;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// The file library's admin API. Every <see cref="FileDescriptorDto.Url"/> it returns is the public read
/// endpoint's address on the host this request came in on (<see cref="SiteFileUrl.Build"/>); behind a
/// gateway, that host is the gateway's, which then has to route <c>/api/site-public/files/</c> here too.
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
    public virtual async Task<FileDescriptorDto> GetAsync(Guid id)
    {
        return WithUrl(await FileAdminAppService.GetAsync(id));
    }

    [HttpGet]
    public virtual async Task<PagedResultDto<FileDescriptorDto>> GetListAsync(GetFilesInput input)
    {
        var result = await FileAdminAppService.GetListAsync(input);
        foreach (var file in result.Items)
        {
            WithUrl(file);
        }

        return result;
    }

    /// <summary>
    /// <c>multipart/form-data</c>: the file under <c>file</c>; <c>containerName</c> and <c>directoryId</c> as
    /// query parameters, so <see cref="FileUploadSizeLimitFilter"/> knows the container before the body is read.
    /// </summary>
    [HttpPost]
    [TypeFilter(typeof(FileUploadSizeLimitFilter))]
    public virtual async Task<FileDescriptorDto> CreateAsync(CreateFileInput input)
    {
        return WithUrl(await FileAdminAppService.CreateAsync(input));
    }

    [HttpPut]
    [Route("{id:guid}")]
    public virtual async Task<FileDescriptorDto> UpdateAsync(Guid id, UpdateFileInput input)
    {
        return WithUrl(await FileAdminAppService.UpdateAsync(id, input));
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

    protected virtual FileDescriptorDto WithUrl(FileDescriptorDto file)
    {
        file.Url = SiteFileUrl.Build($"{Request.Scheme}://{Request.Host.Value}", file.ContainerName, file.BlobName, file.TenantId);
        return file;
    }
}
