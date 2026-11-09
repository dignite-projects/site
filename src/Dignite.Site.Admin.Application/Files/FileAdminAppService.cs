using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Security.Claims;
using Volo.Abp.Threading;
using Volo.Abp.Users;

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
        var cancellationToken = RequestCancellationToken;
        await AuthorizeCreateAsync(input.ContainerName, input.DirectoryId, cancellationToken);

        var fileName = input.File.FileName.IsNullOrWhiteSpace() ? "file" : input.File.FileName!;

        // The content closes what it owns and leaves alone what it was only lent (disposeStream): the request's own body,
        // for an upload that arrives by a link, is the server's to close.
        using var content = input.File;
        var file = await FileDescriptorManager.CreateAsync(
            input.ContainerName,
            fileName,
            content.GetStream(),
            input.DirectoryId,
            cancellationToken);

        return ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(file);
    }

    /// <summary>
    /// Whether the caller may upload to <paramref name="containerName"/> now: the container is one of the site's, the
    /// container's permission is granted, and the directory is the caller's, in the current tenant. Checked before anything is
    /// stored, against a stand-in that carries what the container's rules look at - the same check for the upload and for the
    /// link that will make one later, so a link is never issued for an upload that would be refused.
    /// </summary>
    protected virtual async Task AuthorizeCreateAsync(string containerName, Guid? directoryId, CancellationToken cancellationToken)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(containerName);

        var pending = new FileDescriptor(
            GuidGenerator.Create(), containerName, "pending", "pending", string.Empty, 0,
            hash: null, referBlobName: null, directoryId, CurrentTenant.Id);
        await AuthorizationService.CheckAsync(pending, FileOperations.Create);

        // The new file is the caller's, in the current tenant: the directory must be theirs, there.
        await EnsureDirectoryFitsAsync(directoryId, containerName, CurrentUser.Id, CurrentTenant.Id, cancellationToken);
    }

    /// <summary>
    /// Authorized now exactly as the upload would be - the container, the permission, the directory - so a link is never
    /// issued for an upload that would be refused; and authorized again when it is used, under the identity it carries.
    /// </summary>
    public virtual async Task<FileUploadTicketDto> CreateUploadTicketAsync(CreateFileUploadTicketInput input)
    {
        await AuthorizeCreateAsync(input.ContainerName, input.DirectoryId, RequestCancellationToken);

        var expiresAt = Clock.Now.Add(FileUploadTicketProtector.Lifetime);
        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(BlobContainerConfigurationProvider.Get(input.ContainerName));

        var ticket = new FileUploadTicket(
            FileUploadTicket.CurrentVersion,
            Guid.NewGuid().ToString("N"),
            new DateTimeOffset(expiresAt),
            CurrentTenant.Id,
            CurrentUser.Id,
            CurrentUser.UserName,
            CurrentUser.Roles,
            CurrentUser.FindClaimValue(AbpClaimTypes.ClientId),
            input.ContainerName,
            input.FileName.Trim(),
            input.DirectoryId,
            maxBytes);

        var protectedTicket = LazyServiceProvider.LazyGetRequiredService<FileUploadTicketProtector>().Protect(ticket);

        return new FileUploadTicketDto
        {
            Path = FileUploadTicketPaths.Build(protectedTicket),
            ExpiresAt = expiresAt,
            MaxBytes = maxBytes,
            ContainerName = ticket.ContainerName,
            FileName = ticket.FileName
        };
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
        var authorization = configuration.GetAuthorizationConfiguration();

        // What the container does to an image, read from the contributors it was configured with, not restated.
        var resize = configuration.PipelineContributors.Contains<ImageResizeContributor>()
            ? configuration.GetImageResizeContributorConfiguration()
            : null;

        return Task.FromResult(new FileContainerConfigurationDto
        {
            // What the upload actually enforces: the container's MaxSize contributor, or the pipeline's
            // default buffering cap without one (FileDescriptorManager buffers to the same bound).
            MaxBlobSize = BlobStreamBuffering.GetMaxBufferedBytes(configuration),
            // The container allows MIME types (checked against the detected content); a file input and the
            // MCP tools want extensions, so they are mapped back through Site's own type table. Empty when the
            // container restricts no type.
            AllowedFileTypeNames = SiteFileTypes.GetExtensions(
                configuration.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes),
            ImageMaxWidth = resize is { MaxWidth: > 0 } ? resize.MaxWidth : null,
            ImageMaxHeight = resize is { MaxHeight: > 0 } ? resize.MaxHeight : null,
            // Re-encoded either way: ABP's compressor in the picture's own format, or a JPEG stored as WebP.
            CompressesImages = configuration.PipelineContributors.Contains<ImageCompressContributor>() ||
                               configuration.PipelineContributors.Contains<JpegToWebpContributor>(),
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
