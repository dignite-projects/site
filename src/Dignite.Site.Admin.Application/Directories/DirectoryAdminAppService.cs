using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Dignite.Site.Directories;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.Site.Admin.Directories;

[Authorize]
public class DirectoryAdminAppService : SiteAdminAppService, IDirectoryAdminAppService
{
    protected DirectoryManager DirectoryManager { get; }

    protected IDirectoryDescriptorRepository DirectoryDescriptorRepository { get; }

    protected CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    public DirectoryAdminAppService(
        DirectoryManager directoryManager,
        IDirectoryDescriptorRepository directoryDescriptorRepository)
    {
        DirectoryManager = directoryManager;
        DirectoryDescriptorRepository = directoryDescriptorRepository;
    }

    public virtual async Task<DirectoryDescriptorDto> GetAsync(Guid id)
    {
        var directory = await DirectoryDescriptorRepository.GetAsync(id, cancellationToken: RequestCancellationToken);
        await AuthorizationService.CheckAsync(directory, FileOperations.Get);
        return ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(directory);
    }

    public virtual async Task<ListResultDto<DirectoryDescriptorInfoDto>> GetListAsync(GetDirectoriesInput input)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(input.ContainerName);

        var directories = await DirectoryDescriptorRepository.GetAllByUserAsync(
            GetCurrentUserId(),
            input.ContainerName,
            RequestCancellationToken);
        var nodes = ObjectMapper.Map<List<DirectoryDescriptor>, List<DirectoryDescriptorInfoDto>>(directories);

        return new ListResultDto<DirectoryDescriptorInfoDto>(nodes.BuildTree());
    }

    public virtual async Task<DirectoryDescriptorDto> CreateAsync(CreateDirectoryInput input)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(input.ContainerName);
        var userId = GetCurrentUserId();

        var pending = new DirectoryDescriptor(
            GuidGenerator.Create(), input.ContainerName, input.Name, input.ParentId, 0, CurrentTenant.Id)
        {
            CreatorId = userId
        };
        await AuthorizationService.CheckAsync(pending, FileOperations.Create);

        var directory = await DirectoryManager.CreateAsync(
            userId,
            input.ContainerName,
            input.Name,
            input.ParentId,
            RequestCancellationToken);

        return ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(directory);
    }

    public virtual async Task<DirectoryDescriptorDto> UpdateAsync(Guid id, UpdateDirectoryInput input)
    {
        var cancellationToken = RequestCancellationToken;
        var directory = await DirectoryDescriptorRepository.GetAsync(id, false, cancellationToken);
        await AuthorizationService.CheckAsync(directory, FileOperations.Update);

        await DirectoryManager.UpdateAsync(directory, input.Name, cancellationToken);
        return ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(directory);
    }

    public virtual async Task<DirectoryDescriptorDto> MoveAsync(Guid id, MoveDirectoryInput input)
    {
        var cancellationToken = RequestCancellationToken;
        var directory = await DirectoryDescriptorRepository.GetAsync(id, false, cancellationToken);
        await AuthorizationService.CheckAsync(directory, FileOperations.Update);

        if (input.ParentId.HasValue)
        {
            var parent = await DirectoryDescriptorRepository.GetAsync(input.ParentId.Value, false, cancellationToken);
            await AuthorizationService.CheckAsync(parent, FileOperations.Update);
        }

        directory = await DirectoryManager.MoveAsync(directory, input.ParentId, input.Order, cancellationToken);
        return ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(directory);
    }

    /// <summary>
    /// Transactional: <see cref="DirectoryManager.DeleteAsync"/>'s emptiness check and the delete must not be
    /// separated by another request's write.
    /// </summary>
    [UnitOfWork(isTransactional: true)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var cancellationToken = RequestCancellationToken;
        var directory = await DirectoryDescriptorRepository.GetAsync(id, cancellationToken: cancellationToken);
        await AuthorizationService.CheckAsync(directory, FileOperations.Delete);
        await DirectoryManager.DeleteAsync(directory, cancellationToken);
    }

    /// <summary>
    /// Directories are per user. A caller authenticated without one - a client-credentials token - has none
    /// to list or own, and is refused with that reason.
    /// </summary>
    protected virtual Guid GetCurrentUserId()
    {
        return CurrentUser.Id ?? throw new AbpAuthorizationException(code: SiteErrorCodes.DirectoryRequiresUser);
    }
}
