using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Directories;

/// <summary>
/// The caller's own directory tree in one of Site's file containers. Directories belong to the user who
/// created them, so every operation needs a signed-in user (a client credential is refused) and acts only on
/// that user's directories.
/// </summary>
public interface IDirectoryAdminAppService : IApplicationService
{
    Task<DirectoryDescriptorDto> GetAsync(Guid id);

    /// <summary>The caller's whole tree in the container, as nested <see cref="DirectoryDescriptorInfoDto.Children"/>.</summary>
    Task<ListResultDto<DirectoryDescriptorInfoDto>> GetListAsync(GetDirectoriesInput input);

    Task<DirectoryDescriptorDto> CreateAsync(CreateDirectoryInput input);

    /// <summary>Renames the directory.</summary>
    Task<DirectoryDescriptorDto> UpdateAsync(Guid id, UpdateDirectoryInput input);

    Task<DirectoryDescriptorDto> MoveAsync(Guid id, MoveDirectoryInput input);

    /// <summary>Deletes an empty directory; one with subdirectories or files is refused.</summary>
    Task DeleteAsync(Guid id);
}
