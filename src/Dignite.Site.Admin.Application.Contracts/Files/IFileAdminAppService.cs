using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// What Site's administration UI and MCP tools do with the site's files, which Blob Management keeps. Blob Management
/// authorizes no user: it is a trusted, in-process caller of the file service, so Site applies its own rules here - who
/// may put a file in which container - before it calls.
/// <para>
/// A file is uploaded in three steps, none of which carries the bytes through Site: <see cref="CreateUploadTicketAsync"/>
/// asks Blob Management for a one-use ticket, the browser (or the MCP client) posts the file to the ticket's
/// <see cref="SiteFileUploadTicketDto.UploadUrl"/>, and <see cref="ConfirmAsync"/> files it - until then it is pending,
/// is read by nobody and is deleted by Blob Management after a day.
/// </para>
/// </summary>
public interface IFileAdminAppService : IApplicationService
{
    /// <summary>The containers the caller may upload to, with the limits Blob Management applies to them.</summary>
    Task<ListResultDto<SiteFileContainerDto>> GetContainersAsync();

    /// <summary>The files of one container the caller may upload to, a page at a time; pending files are left out.</summary>
    Task<PagedResultDto<SiteFileDto>> GetListAsync(GetSiteFilesInput input);

    /// <summary>One file, also a pending one the caller uploaded.</summary>
    Task<SiteFileDto> GetAsync(Guid id);

    /// <summary>A ticket for the caller's browser to post one file to a container with.</summary>
    Task<SiteFileUploadTicketDto> CreateUploadTicketAsync(CreateSiteFileUploadTicketInput input);

    /// <summary>
    /// Confirms a file the caller uploaded with a ticket, and marks it public: every file of Site's containers is read by
    /// the site's visitors, at <see cref="SiteFileDto.Url"/>.
    /// </summary>
    Task<SiteFileDto> ConfirmAsync(Guid id);

    /// <summary>
    /// Deletes a file: the one the caller uploaded, or any of the container's with the permission that deletes there. A
    /// file that is not there is no error.
    /// </summary>
    Task DeleteAsync(Guid id);
}
