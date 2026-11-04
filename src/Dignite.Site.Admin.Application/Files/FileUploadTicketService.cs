using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace Dignite.Site.Admin.Files;

/// <inheritdoc cref="IFileUploadTicketService"/>
/// <remarks>
/// The upload itself is <see cref="IFileAdminAppService.CreateAsync"/>, called with the identity and the tenant the ticket
/// carries: the container's permission, the directory's owner, the pipeline's size and type rules all apply as they do to
/// a user uploading with a token. The ticket decides only <i>who</i> and <i>what</i> - container, name, directory,
/// size - and the caller of the endpoint decides none of it.
/// </remarks>
public class FileUploadTicketService : IFileUploadTicketService, ITransientDependency
{
    protected FileUploadTicketProtector TicketProtector { get; }

    protected IFileAdminAppService FileAppService { get; }

    protected ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public FileUploadTicketService(
        FileUploadTicketProtector ticketProtector,
        IFileAdminAppService fileAppService,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        ICurrentTenant currentTenant)
    {
        TicketProtector = ticketProtector;
        FileAppService = fileAppService;
        CurrentPrincipalAccessor = currentPrincipalAccessor;
        CurrentTenant = currentTenant;
    }

    public virtual async Task<FileDescriptorDto> UploadAsync(string ticket, Func<long, FileUploadBody> openBody)
    {
        var read = TicketProtector.Read(ticket);

        // Spent before anything else is done with it: two requests racing with one link store one file.
        if (read == null || !await TicketProtector.TryUseAsync(read))
        {
            throw new BusinessException(SiteErrorCodes.FileUploadTicketInvalid);
        }

        var body = openBody(read.MaxBytes);

        using (CurrentPrincipalAccessor.Change(read.ToPrincipal()))
        using (CurrentTenant.Change(read.TenantId))
        {
            return await FileAppService.CreateAsync(new CreateFileInput
            {
                ContainerName = read.ContainerName,
                DirectoryId = read.DirectoryId,
                // The type is never taken from the sender: the pipeline detects it from the bytes.
                // Lent, not given: the body is the request's, so the content must not close it when the upload is done.
                File = new RemoteStreamContent(body.Stream, read.FileName, "application/octet-stream", body.Length, disposeStream: false)
            });
        }
    }
}
