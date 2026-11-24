using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Uploads;
using Dignite.Site.Admin.Files;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Abp.BlobManagement.Public;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// Site's files through <see cref="IFileAdminAppService"/>, in front of Blob Management: the three steps of an upload
/// (a ticket, the browser's post, the confirmation), what the site's visitors can read afterwards, and the files that are
/// not Site's.
/// </summary>
public class FileAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFileAdminAppService _files;
    private readonly IBlobManagementUploadAppService _uploads;
    private readonly IBlobManagementFileService _blobManagementFiles;
    private readonly IBlobManagementPublicAppService _publicFiles;

    public FileAdminAppService_Tests()
    {
        _files = GetRequiredService<IFileAdminAppService>();
        _uploads = GetRequiredService<IBlobManagementUploadAppService>();
        _blobManagementFiles = GetRequiredService<IBlobManagementFileService>();
        _publicFiles = GetRequiredService<IBlobManagementPublicAppService>();
    }

    [Fact]
    public async Task Should_List_Site_Containers_With_Their_Limits()
    {
        var containers = await AsEditorAsync(() => _files.GetContainersAsync());

        containers.Items.Select(container => container.Name)
            .ShouldBe(SiteFileContainerNames.All, ignoreOrder: true);
        containers.Items.ShouldAllBe(container => container.MaxFileSize > 0);
    }

    /// <summary>
    /// A caller with no user - an MCP client signed in with client credentials, or Site's own back end calling a Blob
    /// Management that runs apart - lists the containers with their limits too: Blob Management answers a client for a
    /// container's limits (from 10.7.7-preview.3).
    /// </summary>
    [Fact]
    public async Task Should_List_Site_Containers_With_Their_Limits_For_A_Client_Without_A_User()
    {
        var principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        var client = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.ClientId, "Site_Mcp_Service")
        }));

        using (principalAccessor.Change(client))
        {
            var containers = await WithUnitOfWorkAsync(() => _files.GetContainersAsync());

            containers.Items.Select(container => container.Name)
                .ShouldBe(SiteFileContainerNames.All, ignoreOrder: true);
            containers.Items.ShouldAllBe(container => container.MaxFileSize > 0);
        }
    }

    [Fact]
    public async Task An_Upload_Is_Pending_Until_It_Is_Confirmed_And_Then_Public_At_Sites_Address()
    {
        var bytes = TestFiles.Pdf("admin");

        var ticket = await AsEditorAsync(() =>
            _files.CreateUploadTicketAsync(new CreateSiteFileUploadTicketInput { ContainerName = SiteFileContainerNames.Attachments }));
        ticket.UploadUrl.ShouldContain("/uploads/");

        var uploaded = await AsEditorAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _uploads.UploadAsync(TicketOf(ticket), new RemoteStreamContent(stream, "admin.pdf", "application/pdf"));
        });
        uploaded.Id.ShouldBe(ticket.FileId);
        uploaded.IsPending.ShouldBeTrue();

        // Pending: the uploader sees it, with no address, and the site's visitors cannot read it.
        var pending = await AsEditorAsync(() => _files.GetAsync(ticket.FileId));
        pending.IsPending.ShouldBeTrue();
        pending.Url.ShouldBeNull();
        (await AsEditorAsync(() => _files.GetListAsync(new GetSiteFilesInput { ContainerName = SiteFileContainerNames.Attachments })))
            .Items.ShouldBeEmpty();

        var confirmed = await AsEditorAsync(() => _files.ConfirmAsync(ticket.FileId));

        confirmed.IsPending.ShouldBeFalse();
        confirmed.Name.ShouldBe("admin.pdf");
        confirmed.MimeType.ShouldBe("application/pdf");
        confirmed.Size.ShouldBe(bytes.Length);
        confirmed.Url.ShouldNotBeNull();
        confirmed.Url!.ShouldStartWith("/blobs/" + SiteFileContainerNames.Attachments + "/");

        var read = await _publicFiles.ReadAsync(
            SiteFileContainerNames.Attachments, BlobNameOf(confirmed.Url), new BlobManagementPublicReadInput(), new BlobManagementPublicReadRequest());
        using var memory = new MemoryStream();
        await read.Content!.GetStream().CopyToAsync(memory);
        memory.ToArray().ShouldBe(bytes);

        (await AsEditorAsync(() => _files.GetListAsync(new GetSiteFilesInput { ContainerName = SiteFileContainerNames.Attachments })))
            .Items.Select(file => file.Id).ShouldBe(new[] { ticket.FileId });
    }

    [Fact]
    public async Task Confirming_Twice_Changes_Nothing()
    {
        var file = await UploadAndConfirmAsync(SiteFileContainerNames.Attachments, "twice.pdf", TestFiles.Pdf("twice"));

        var again = await AsEditorAsync(() => _files.ConfirmAsync(file.Id));

        again.Url.ShouldBe(file.Url);
        again.IsPending.ShouldBeFalse();
    }

    [Fact]
    public async Task A_Deleted_File_Is_Gone_And_Deleting_It_Again_Is_No_Error()
    {
        var file = await UploadAndConfirmAsync(SiteFileContainerNames.Attachments, "gone.pdf", TestFiles.Pdf("gone"));

        await AsEditorAsync(() => _files.DeleteAsync(file.Id));
        await AsEditorAsync(() => _files.DeleteAsync(file.Id));

        await Should.ThrowAsync<EntityNotFoundException>(() => _publicFiles.ReadAsync(
            SiteFileContainerNames.Attachments, BlobNameOf(file.Url!), new BlobManagementPublicReadInput(), new BlobManagementPublicReadRequest()));
    }

    [Fact]
    public async Task A_Deleted_File_Is_Deleted_For_Good_Not_Soft_Deleted()
    {
        var file = await UploadAndConfirmAsync(SiteFileContainerNames.Attachments, "for-good.pdf", TestFiles.Pdf("for good"));

        await AsEditorAsync(() => _files.DeleteAsync(file.Id));

        // Blob Management's default keeps a soft-deleted file's row and bytes; Site has no restore, so none is left.
        var remaining = await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<IDataFilter<ISoftDelete>>().Disable())
            {
                return await GetRequiredService<IFileDescriptorRepository>().FindAsync(file.Id);
            }
        });
        remaining.ShouldBeNull();
    }

    [Fact]
    public async Task A_Container_That_Is_Not_Sites_Is_Refused()
    {
        await Should.ThrowAsync<Exception>(() =>
            AsEditorAsync(() => _files.CreateUploadTicketAsync(new CreateSiteFileUploadTicketInput { ContainerName = "invoices" })));
    }

    [Fact]
    public async Task An_Unknown_File_Is_Not_Found()
    {
        var id = Guid.NewGuid();
        await Should.ThrowAsync<EntityNotFoundException>(() => AsEditorAsync(() => _files.GetAsync(id)));
    }

    /// <summary>
    /// The call runs as a signed-in editor - the tests of the test base run with no user - because an upload is counted
    /// for, and may be deleted by, its uploader.
    /// </summary>
    private async Task<T> AsEditorAsync<T>(Func<Task<T>> func)
    {
        var principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        var editor = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, EditorId.ToString()),
            new Claim(AbpClaimTypes.UserName, "editor")
        }));

        using (principalAccessor.Change(editor))
        {
            return await base.WithUnitOfWorkAsync(func);
        }
    }

    private async Task AsEditorAsync(Func<Task> func)
    {
        await AsEditorAsync(async () =>
        {
            await func();
            return true;
        });
    }

    private static readonly Guid EditorId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

    private async Task<SiteFileDto> UploadAndConfirmAsync(string containerName, string name, byte[] bytes)
    {
        var ticket = await AsEditorAsync(() =>
            _files.CreateUploadTicketAsync(new CreateSiteFileUploadTicketInput { ContainerName = containerName }));

        await AsEditorAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _uploads.UploadAsync(TicketOf(ticket), new RemoteStreamContent(stream, name));
        });

        return await AsEditorAsync(() => _files.ConfirmAsync(ticket.FileId));
    }

    /// <summary>The ticket is the last segment of the upload address.</summary>
    private static string TicketOf(SiteFileUploadTicketDto ticket)
    {
        return ticket.UploadUrl[(ticket.UploadUrl.LastIndexOf('/') + 1)..];
    }

    /// <summary>The physical BLOB name in Blob Management's public address, <c>/blobs/{container}/{blob}[?...]</c>.</summary>
    private static string BlobNameOf(string url)
    {
        var path = url.Split('?')[0];
        return path[(path.LastIndexOf('/') + 1)..];
    }
}
