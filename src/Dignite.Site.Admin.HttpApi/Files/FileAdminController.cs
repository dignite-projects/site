using System;
using System.Threading.Tasks;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
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

    protected IFileUploadTicketService FileUploadTicketService { get; }

    public FileAdminController(IFileAdminAppService fileAdminAppService, IFileUploadTicketService fileUploadTicketService)
    {
        FileAdminAppService = fileAdminAppService;
        FileUploadTicketService = fileUploadTicketService;
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

    /// <summary>
    /// A link to <see cref="UploadByTicketAsync"/>: for a client that has the file at hand but cannot send its bytes
    /// through this API call.
    /// </summary>
    [HttpPost]
    [Route("upload-tickets")]
    public virtual Task<FileUploadTicketDto> CreateUploadTicketAsync(CreateFileUploadTicketInput input)
    {
        return FileAdminAppService.CreateUploadTicketAsync(input);
    }

    /// <summary>
    /// Stores the file sent as the raw body (<c>PUT</c>, any content type) under the link the ticket makes. Anonymous:
    /// the ticket is the credential - signed, short-lived, single use, and bound to a container, a name and a size. The
    /// body is bounded by the ticket's limit before it is read.
    /// </summary>
    [HttpPut]
    [Route(FileUploadTicketPaths.RouteTemplate)]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [DisableFormValueModelBinding]
    public virtual async Task<FileDescriptorDto> UploadByTicketAsync(string ticket)
    {
        return await FileUploadTicketService.UploadAsync(ticket, maxBytes =>
        {
            var feature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = maxBytes;
            }

            return new FileUploadBody(Request.Body, Request.ContentLength);
        });
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
