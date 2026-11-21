using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Site's files over HTTP. Who may upload to which container is decided in <see cref="IFileAdminAppService"/>, per
/// container, so the only gate here is a signed-in user.
/// </summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize]
[Route("api/site-admin/files")]
public class FileAdminController : SiteAdminController, IFileAdminAppService
{
    protected IFileAdminAppService FileAppService { get; }

    public FileAdminController(IFileAdminAppService fileAppService)
    {
        FileAppService = fileAppService;
    }

    [HttpGet("containers")]
    public virtual Task<ListResultDto<SiteFileContainerDto>> GetContainersAsync()
    {
        return FileAppService.GetContainersAsync();
    }

    [HttpGet]
    public virtual Task<PagedResultDto<SiteFileDto>> GetListAsync(GetSiteFilesInput input)
    {
        return FileAppService.GetListAsync(input);
    }

    [HttpGet("{id}")]
    public virtual Task<SiteFileDto> GetAsync(Guid id)
    {
        return FileAppService.GetAsync(id);
    }

    [HttpPost("upload-tickets")]
    public virtual Task<SiteFileUploadTicketDto> CreateUploadTicketAsync(CreateSiteFileUploadTicketInput input)
    {
        return FileAppService.CreateUploadTicketAsync(input);
    }

    [HttpPost("{id}/confirm")]
    public virtual Task<SiteFileDto> ConfirmAsync(Guid id)
    {
        return FileAppService.ConfirmAsync(id);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return FileAppService.DeleteAsync(id);
    }
}
