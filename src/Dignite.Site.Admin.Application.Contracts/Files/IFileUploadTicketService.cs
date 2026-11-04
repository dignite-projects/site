using System;
using System.IO;
using System.Threading.Tasks;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Uses an upload link: <see cref="IFileAdminAppService.CreateUploadTicketAsync"/> issues it, this stores the file it
/// was sent. Not an application service on purpose - it has no permission of its own and is not offered as an API
/// operation; the endpoint that receives the bytes (<c>FileAdminController.UploadByTicketAsync</c>) calls it.
/// </summary>
public interface IFileUploadTicketService
{
    /// <summary>
    /// Stores the file the link was used to send, as the identity the link was issued to, with every check an ordinary
    /// upload has. The link is spent by the first call, whether the upload then succeeds or not.
    /// </summary>
    /// <param name="ticket">The text of the link, as it was in its path.</param>
    /// <param name="openBody">
    /// Called once the link is accepted, with the most bytes it takes, to open the body: the caller bounds the request
    /// by that before it reads anything.
    /// </param>
    /// <exception cref="Volo.Abp.BusinessException">
    /// <see cref="SiteErrorCodes.FileUploadTicketInvalid"/>: not a link this site issued, expired, or already used.
    /// </exception>
    Task<FileDescriptorDto> UploadAsync(string ticket, Func<long, FileUploadBody> openBody);
}

/// <summary>The body of an upload: its stream and, when the transport said, its length.</summary>
public sealed record FileUploadBody(Stream Stream, long? Length);
