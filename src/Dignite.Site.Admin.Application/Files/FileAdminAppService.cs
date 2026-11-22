using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Site.Files;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Site's files are Blob Management's: this service is the rule in front of it (<see cref="SiteFileAuthorization"/>) and
/// the shape Site's UI and MCP tools want - Site's address of a file, not Blob Management's. It stores nothing itself.
/// <para>
/// Only the three containers of <see cref="SiteFileContainerNames"/> are reachable from here, whatever else the host
/// declares. A file is uploaded with Blob Management's upload ticket and filed with <see cref="ConfirmAsync"/>; a file
/// nobody confirms is deleted by Blob Management after <c>BlobManagementUploadOptions.PendingLifetime</c>.
/// </para>
/// </summary>
public class FileAdminAppService : SiteAdminAppService, IFileAdminAppService
{
    protected IBlobManagementFileService BlobManagementFileService { get; }

    protected IBlobManagementContainerAppService BlobManagementContainerAppService { get; }

    protected SiteFileAuthorization Authorization { get; }

    public FileAdminAppService(
        IBlobManagementFileService blobManagementFileService,
        IBlobManagementContainerAppService blobManagementContainerAppService,
        SiteFileAuthorization authorization)
    {
        BlobManagementFileService = blobManagementFileService;
        BlobManagementContainerAppService = blobManagementContainerAppService;
        Authorization = authorization;
    }

    public virtual async Task<ListResultDto<SiteFileContainerDto>> GetContainersAsync()
    {
        var containers = new System.Collections.Generic.List<SiteFileContainerDto>();

        foreach (var name in SiteFileContainerNames.All)
        {
            if (!await Authorization.CanUploadAsync(name))
            {
                continue;
            }

            var container = await BlobManagementContainerAppService.GetAsync(name);
            containers.Add(new SiteFileContainerDto
            {
                Name = container.Name,
                MaxFileSize = container.MaxFileSize,
                AllowedFileExtensions = container.AllowedFileExtensions,
                ImageMaxWidth = container.ImageMaxWidth,
                ImageMaxHeight = container.ImageMaxHeight
            });
        }

        return new ListResultDto<SiteFileContainerDto>(containers);
    }

    public virtual async Task<PagedResultDto<SiteFileDto>> GetListAsync(GetSiteFilesInput input)
    {
        await Authorization.CheckUploadAsync(input.ContainerName);

        var files = await BlobManagementFileService.GetListAsync(new GetBlobManagementFilesInput
        {
            ContainerName = input.ContainerName,
            Filter = input.Filter,
            DirectoryId = input.DirectoryId,
            Sorting = input.Sorting,
            SkipCount = input.SkipCount,
            MaxResultCount = input.MaxResultCount
        });

        return new PagedResultDto<SiteFileDto>(files.TotalCount, files.Items.Select(Map).ToList());
    }

    public virtual async Task<SiteFileDto> GetAsync(Guid id)
    {
        var file = await BlobManagementFileService.GetAsync(id);
        await CheckReadableAsync(file);

        return Map(file);
    }

    public virtual async Task<SiteFileUploadTicketDto> CreateUploadTicketAsync(CreateSiteFileUploadTicketInput input)
    {
        await Authorization.CheckUploadAsync(input.ContainerName);

        var ticket = await BlobManagementFileService.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput
        {
            ContainerName = input.ContainerName,
            DirectoryId = input.DirectoryId,
            UploaderId = CurrentUser.Id
        });

        return new SiteFileUploadTicketDto
        {
            FileId = ticket.FileId,
            UploadUrl = ticket.UploadUrl,
            ExpirationTime = ticket.ExpirationTime
        };
    }

    public virtual async Task<SiteFileDto> ConfirmAsync(Guid id)
    {
        var file = await BlobManagementFileService.GetAsync(id);
        await Authorization.CheckUploadAsync(RequireSiteContainer(file));

        // The file is the caller's own upload, which the ticket recorded: a pending file is nobody else's to file.
        if (file.IsPending && file.CreatorId != CurrentUser.Id)
        {
            throw new AbpAuthorizationException();
        }

        if (file.IsPending)
        {
            file = await BlobManagementFileService.ConfirmAsync(id);
        }

        if (!file.IsPublic)
        {
            file = await BlobManagementFileService.SetPublicAsync(id, new SetBlobManagementFilePublicInput { IsPublic = true });
        }

        return Map(file);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        BlobManagementFileDto file;
        try
        {
            file = await BlobManagementFileService.GetAsync(id);
        }
        catch (EntityNotFoundException)
        {
            return;
        }

        if (!SiteFileContainerNames.Contains(file.ContainerName))
        {
            return;
        }

        if (!await Authorization.CanDeleteAsync(file.ContainerName, file.CreatorId, CurrentUser.Id))
        {
            throw new AbpAuthorizationException();
        }

        await BlobManagementFileService.DeleteAsync(id);
    }

    /// <summary>
    /// A file of one of Site's containers the caller may upload to; a pending one only its uploader sees. Anything else
    /// is not Site's to show, and is not found.
    /// </summary>
    protected virtual async Task CheckReadableAsync(BlobManagementFileDto file)
    {
        await Authorization.CheckUploadAsync(RequireSiteContainer(file));

        if (file.IsPending && file.CreatorId != CurrentUser.Id)
        {
            throw new AbpAuthorizationException();
        }
    }

    protected virtual string RequireSiteContainer(BlobManagementFileDto file)
    {
        if (!SiteFileContainerNames.Contains(file.ContainerName))
        {
            throw new EntityNotFoundException(typeof(BlobManagementFileDto), file.Id);
        }

        return file.ContainerName;
    }

    protected virtual SiteFileDto Map(BlobManagementFileDto file)
    {
        return new SiteFileDto
        {
            Id = file.Id,
            ContainerName = file.ContainerName,
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size,
            Url = file.Url,
            IsPending = file.IsPending,
            CreatorId = file.CreatorId,
            CreationTime = file.CreationTime
        };
    }
}
