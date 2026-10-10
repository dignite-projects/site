using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Site's file library: browse, upload, rename, move and delete files in Site's file containers. Reading a
/// file's bytes is the public read endpoint's job (<c>IFilePublicAppService</c>), not this service's.
/// <para>
/// Each operation is authorized per container (<c>SiteAdminApplicationModule</c> maps upload, update and
/// delete to <c>SiteAdmin.Contents.*</c>); the uploader may always change or delete their own files, and
/// <c>SiteAdmin.Contents</c> lets a user list and manage everyone's.
/// </para>
/// </summary>
public interface IFileAdminAppService : IApplicationService
{
    Task<FileDescriptorDto> GetAsync(Guid id);

    Task<PagedResultDto<FileDescriptorDto>> GetListAsync(GetFilesInput input);

    Task<FileDescriptorDto> CreateAsync(CreateFileInput input);

    Task<FileDescriptorDto> UpdateAsync(Guid id, UpdateFileInput input);

    Task DeleteAsync(Guid id);

    Task<FileContainerConfigurationDto> GetContainerConfigurationAsync(string containerName);
}
