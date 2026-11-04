using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Files;
using Dignite.Site.Timing;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// The upload link: issued to an authorized caller, redeemed once by someone with no credential, and then the file is the
/// issuer's - with the container's rules applying to the bytes as they do to any upload.
/// </summary>
public class FileUploadTicket_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFileAdminAppService _fileAppService;
    private readonly IFileUploadTicketService _ticketService;

    public FileUploadTicket_Tests()
    {
        _fileAppService = GetRequiredService<IFileAdminAppService>();
        _ticketService = GetRequiredService<IFileUploadTicketService>();
    }

    private static string TicketOf(FileUploadTicketDto dto) => dto.Path[(dto.Path.LastIndexOf('/') + 1)..];

    private Task<FileUploadTicketDto> IssueAsync(string container = SiteFileContainerNames.Images, string fileName = "cover.png", Guid? directoryId = null) =>
        _fileAppService.CreateUploadTicketAsync(new CreateFileUploadTicketInput
        {
            ContainerName = container,
            FileName = fileName,
            DirectoryId = directoryId
        });

    private static FileUploadBody Body(byte[] bytes) => new(new MemoryStream(bytes), bytes.LongLength);

    [Fact]
    public async Task Should_Issue_A_Link_With_The_Containers_Limit_And_A_Short_Life()
    {
        var ticket = await IssueAsync();

        ticket.Path.ShouldStartWith("api/site-admin/files/upload/");
        ticket.ContainerName.ShouldBe(SiteFileContainerNames.Images);
        ticket.FileName.ShouldBe("cover.png");
        ticket.MaxBytes.ShouldBeGreaterThan(0);
        ticket.ExpiresAt.ShouldBeGreaterThan(DateTime.Now);
        ticket.ExpiresAt.ShouldBeLessThanOrEqualTo(DateTime.Now.AddMinutes(11));
    }

    /// <summary>
    /// The body is the request's: storing the file must not close it. ABP Studio's development middleware reads the body again
    /// after the action and, finding it closed, dropped the connection after the response - an upload that worked, reported
    /// by curl as an error.
    /// </summary>
    [Fact]
    public async Task Should_Leave_The_Request_Body_Open_After_Storing_The_File()
    {
        var ticket = await IssueAsync();
        var body = new MemoryStream(TestFiles.Png(16, 16));

        await _ticketService.UploadAsync(TicketOf(ticket), _ => new FileUploadBody(body, body.Length));

        body.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Store_The_File_Sent_To_The_Link_Under_The_Ticket_Name_And_Container()
    {
        var ticket = await IssueAsync(fileName: "hero.png");
        var png = TestFiles.Png(16, 16);
        long? limit = null;

        var file = await _ticketService.UploadAsync(TicketOf(ticket), maxBytes =>
        {
            limit = maxBytes;
            return Body(png);
        });

        limit.ShouldBe(ticket.MaxBytes);
        file.Name.ShouldBe("hero.png");
        file.ContainerName.ShouldBe(SiteFileContainerNames.Images);
        file.MimeType.ShouldBe("image/png");
        file.Url.ShouldBe($"/api/site-public/files/{SiteFileContainerNames.Images}/{file.BlobName}");
    }

    /// <summary>The link is as good as its first use, whether or not the upload then goes well.</summary>
    [Fact]
    public async Task Should_Refuse_A_Link_That_Was_Already_Used()
    {
        var ticket = await IssueAsync();
        await _ticketService.UploadAsync(TicketOf(ticket), _ => Body(TestFiles.Png(16, 16)));

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _ticketService.UploadAsync(TicketOf(ticket), _ => Body(TestFiles.Png(16, 16))));

        exception.Code.ShouldBe(SiteErrorCodes.FileUploadTicketInvalid);
    }

    /// <summary>Two requests with one link at the same moment: exactly one of them stores a file.</summary>
    [Fact]
    public async Task Should_Let_Only_One_Of_Two_Simultaneous_Requests_Use_A_Link()
    {
        var ticket = TicketOf(await IssueAsync());

        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try
            {
                await _ticketService.UploadAsync(ticket, __ => Body(TestFiles.Png(16, 16)));
                return true;
            }
            catch (BusinessException exception) when (exception.Code == SiteErrorCodes.FileUploadTicketInvalid)
            {
                return false;
            }
        }));

        attempts.Count(used => used).ShouldBe(1);
    }

    [Fact]
    public async Task Should_Spend_The_Link_Even_When_The_Upload_Is_Refused()
    {
        var ticket = await IssueAsync(fileName: "notes.png");

        // Text named .png: the type is detected from the bytes, and they are not a picture.
        await Should.ThrowAsync<BusinessException>(
            () => _ticketService.UploadAsync(TicketOf(ticket), _ => Body("not a picture"u8.ToArray())));

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _ticketService.UploadAsync(TicketOf(ticket), _ => Body(TestFiles.Png(16, 16))));
        exception.Code.ShouldBe(SiteErrorCodes.FileUploadTicketInvalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-ticket")]
    [InlineData("CfDJ8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Should_Refuse_Something_That_Is_Not_A_Link_This_Site_Issued(string ticket)
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _ticketService.UploadAsync(ticket, _ => Body(TestFiles.Png(16, 16))));

        exception.Code.ShouldBe(SiteErrorCodes.FileUploadTicketInvalid);
    }

    [Fact]
    public async Task Should_Refuse_A_Link_With_One_Character_Changed()
    {
        var text = TicketOf(await IssueAsync());
        var changed = text[..^2] + (text[^2] == 'A' ? 'B' : 'A') + text[^1];

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _ticketService.UploadAsync(changed, _ => Body(TestFiles.Png(16, 16))));

        exception.Code.ShouldBe(SiteErrorCodes.FileUploadTicketInvalid);
    }

    [Fact]
    public async Task Should_Refuse_A_Link_After_It_Expired()
    {
        var ticket = await IssueAsync();
        GetRequiredService<TestClock>().Set(DateTime.Now.AddMinutes(30));

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _ticketService.UploadAsync(TicketOf(ticket), _ => Body(TestFiles.Png(16, 16))));

        exception.Code.ShouldBe(SiteErrorCodes.FileUploadTicketInvalid);
    }

    /// <summary>The container's own rules judge the bytes: a link does not widen what a container takes.</summary>
    [Fact]
    public async Task Should_Apply_The_Containers_Type_Rules_To_What_Is_Sent()
    {
        var ticket = await IssueAsync(SiteFileContainerNames.Attachments, "photo.png");

        await Should.ThrowAsync<BusinessException>(
            () => _ticketService.UploadAsync(TicketOf(ticket), _ => Body(TestFiles.Png(16, 16))));
    }

    [Fact]
    public async Task Should_Refuse_To_Issue_A_Link_For_A_Container_That_Is_Not_The_Sites()
    {
        await Should.ThrowAsync<BusinessException>(() => IssueAsync(container: "other-module"));
    }

    /// <summary>The upload runs as the one who asked for the link, not as whoever holds it.</summary>
    [Fact]
    public async Task Should_Store_The_File_As_The_User_Who_Asked_For_The_Link()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, "editor") },
            "Test"));

        FileUploadTicketDto ticket;
        using (GetRequiredService<ICurrentPrincipalAccessor>().Change(principal))
        {
            ticket = await IssueAsync();
        }

        // Redeemed with no user at all, as a command line would.
        var file = await _ticketService.UploadAsync(TicketOf(ticket), _ => Body(TestFiles.Png(16, 16)));

        file.CreatorId.ShouldBe(userId);
    }

    [Fact]
    public async Task The_Tool_Should_Hand_Over_The_Link_With_A_Command_To_Run()
    {
        var options = GetRequiredService<IOptions<SiteMcpFileOptions>>().Value;
        options.UploadBaseUrl = "https://acme.example/";

        var upload = await GetRequiredService<FileTools>().CreateUploadUrlAsync(SiteFileContainerNames.Images, "hero.png");

        upload.Url.ShouldStartWith("https://acme.example/api/site-admin/files/upload/");
        upload.Method.ShouldBe("PUT");
        upload.ContainerName.ShouldBe(SiteFileContainerNames.Images);
        upload.FileName.ShouldBe("hero.png");
        upload.MaxBytes.ShouldBeGreaterThan(0);
        upload.Command.ShouldContain("FILE_PATH");
        upload.Command.ShouldContain(upload.Url);
        upload.PowerShellCommand.ShouldContain("FILE_PATH");
        upload.PowerShellCommand.ShouldContain(upload.Url);
        upload.PowerShellCommand.ShouldNotContain("curl");

        // And the link it made works.
        var file = await _ticketService.UploadAsync(upload.Url[(upload.Url.LastIndexOf('/') + 1)..], _ => Body(TestFiles.Png(16, 16)));
        file.Name.ShouldBe("hero.png");
    }

    /// <summary>No address is better than a path that looks like one: outside a request, with nothing configured, it says so.</summary>
    [Fact]
    public async Task The_Tool_Should_Not_Hand_Over_A_Link_It_Cannot_Give_An_Address()
    {
        GetRequiredService<IOptions<SiteMcpFileOptions>>().Value.UploadBaseUrl = null;

        await Should.ThrowAsync<InvalidOperationException>(
            () => GetRequiredService<FileTools>().CreateUploadUrlAsync(SiteFileContainerNames.Images, "hero.png"));
    }

    [Fact]
    public async Task The_Tool_Should_Refuse_A_Container_It_Does_Not_Offer()
    {
        await Should.ThrowAsync<Exception>(
            () => GetRequiredService<FileTools>().CreateUploadUrlAsync("other-module", "hero.png"));
    }
}
