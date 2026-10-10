using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Directories;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Threading;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Site's file library, for the admin UI and the MCP tools. Every call is authorized against the file (or,
/// for an upload, a not-yet-stored stand-in) through <see cref="FileDescriptorAuthorizationHandler"/>, so the
/// per-container permissions of <c>SiteAdminApplicationModule</c> apply the same way to every caller.
/// </summary>
[Authorize]
public class FileAdminAppService : SiteAdminAppService, IFileAdminAppService
{
    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IDirectoryDescriptorRepository DirectoryDescriptorRepository { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    protected CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    public FileAdminAppService(
        IFileDescriptorRepository fileDescriptorRepository,
        IDirectoryDescriptorRepository directoryDescriptorRepository,
        FileDescriptorManager fileDescriptorManager,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        DirectoryDescriptorRepository = directoryDescriptorRepository;
        FileDescriptorManager = fileDescriptorManager;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
    }

    public virtual async Task<FileDescriptorDto> GetAsync(Guid id)
    {
        var file = await FileDescriptorRepository.GetAsync(id, cancellationToken: RequestCancellationToken);
        await AuthorizationService.CheckAsync(file, FileOperations.Get);
        return ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(file);
    }

    /// <summary>
    /// Without <see cref="SiteAdminPermissions.Contents.Default"/> a caller lists only their own files - and a
    /// caller with no user at all (a client-credentials token) owns none, so gets none rather than an
    /// unfiltered list.
    /// </summary>
    public virtual async Task<PagedResultDto<FileDescriptorDto>> GetListAsync(GetFilesInput input)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(input.ContainerName);
        var cancellationToken = RequestCancellationToken;

        var creatorId = input.CreatorId;
        if (!await AuthorizationService.IsGrantedAsync(SiteAdminPermissions.Contents.Default))
        {
            if (!CurrentUser.Id.HasValue)
            {
                return new PagedResultDto<FileDescriptorDto>(0, new List<FileDescriptorDto>());
            }

            creatorId = CurrentUser.Id;
        }

        var count = await FileDescriptorRepository.GetCountAsync(
            input.ContainerName, creatorId, input.DirectoryId, input.Filter, cancellationToken);
        var files = await FileDescriptorRepository.GetListAsync(
            input.ContainerName,
            creatorId,
            input.DirectoryId,
            input.Filter,
            input.Sorting,
            input.MaxResultCount,
            input.SkipCount,
            cancellationToken);

        return new PagedResultDto<FileDescriptorDto>(
            count,
            ObjectMapper.Map<List<FileDescriptor>, List<FileDescriptorDto>>(files));
    }

    public virtual async Task<FileDescriptorDto> CreateAsync(CreateFileInput input)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(input.ContainerName);
        var cancellationToken = RequestCancellationToken;

        // The upload is authorized before anything is stored, against a stand-in that carries what the
        // container's rules look at.
        var pending = new FileDescriptor(
            GuidGenerator.Create(), input.ContainerName, "pending", "pending", string.Empty, 0,
            hash: null, referBlobName: null, input.DirectoryId, CurrentTenant.Id);
        await AuthorizationService.CheckAsync(pending, FileOperations.Create);

        // The new file is the caller's, in the current tenant: the directory must be theirs, there.
        await EnsureDirectoryFitsAsync(input.DirectoryId, input.ContainerName, CurrentUser.Id, CurrentTenant.Id, cancellationToken);

        var fileName = input.File.FileName.IsNullOrWhiteSpace() ? "file" : input.File.FileName!;
        using var stream = input.File.GetStream();
        var file = await FileDescriptorManager.CreateAsync(
            input.ContainerName,
            fileName,
            stream,
            input.DirectoryId,
            cancellationToken);

        return ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(file);
    }

    public virtual async Task<FileDescriptorDto> UpdateAsync(Guid id, UpdateFileInput input)
    {
        var cancellationToken = RequestCancellationToken;
        var file = await FileDescriptorRepository.GetAsync(id, cancellationToken: cancellationToken);

        await AuthorizationService.CheckAsync(file, FileOperations.Update);

        if (input.DirectoryIdSpecified)
        {
            // A moved file stays its uploader's, so the target directory must be the uploader's - whoever
            // moves it.
            await EnsureDirectoryFitsAsync(input.DirectoryId, file.ContainerName, file.CreatorId, file.TenantId, cancellationToken);
            file.MoveToDirectory(input.DirectoryId);
        }

        if (input.Name != null)
        {
            file.Rename(input.Name);
        }

        await FileDescriptorRepository.UpdateAsync(file, cancellationToken: cancellationToken);
        return ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(file);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var cancellationToken = RequestCancellationToken;
        var file = await FileDescriptorRepository.FindAsync(id, false, cancellationToken);
        if (file == null)
        {
            return;
        }

        await AuthorizationService.CheckAsync(file, FileOperations.Delete);
        await FileDescriptorManager.DeleteAsync(file, cancellationToken);
    }

    public virtual Task<FileContainerConfigurationDto> GetContainerConfigurationAsync(string containerName)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(containerName);

        var configuration = BlobContainerConfigurationProvider.Get(containerName);
        var maxFileSize = configuration.GetFileSizeLimitConfiguration().MaxFileSizeInBytes;
        var authorization = configuration.GetAuthorizationConfiguration();

        return Task.FromResult(new FileContainerConfigurationDto
        {
            // What IFileStorer actually enforces: the container's limit, or core's default cap without one.
            MaxBlobSize = maxFileSize > 0 ? maxFileSize : FileConsts.DefaultMaxFileSizeInBytes,
            AllowedFileTypeNames = configuration.GetFileTypeCheckConfiguration().AllowedFileTypeNames ?? [],
            CreateDirectoryPermissionName = authorization.CreateDirectoryPermissionName,
            CreateFilePermissionName = authorization.CreateFilePermissionName,
            UpdateFilePermissionName = authorization.UpdateFilePermissionName,
            DeleteFilePermissionName = authorization.DeleteFilePermissionName,
            GetFilePermissionName = authorization.GetFilePermissionName
        });
    }

    /// <summary>
    /// A file may only sit in a directory that exists and shares its container, owner and tenant - for a new
    /// file as much as a moved one. Anything else is reported as a directory that does not exist, so the
    /// check reveals nothing about other users' directories.
    /// </summary>
    protected virtual async Task EnsureDirectoryFitsAsync(
        Guid? directoryId,
        string containerName,
        Guid? ownerId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (!directoryId.HasValue)
        {
            return;
        }

        var directory = await DirectoryDescriptorRepository.FindAsync(directoryId.Value, false, cancellationToken);
        if (directory == null ||
            !directory.ContainerName.Equals(containerName, StringComparison.Ordinal) ||
            directory.CreatorId != ownerId ||
            directory.TenantId != tenantId)
        {
            throw new DirectoryNotFoundBusinessException();
        }
    }
}
