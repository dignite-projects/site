using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Uploads;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Files;
using Dignite.Site.Mcp.Files;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Content;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// The file tools, the way an MCP client drives them: the containers, a link to upload with, the file kept once it is
/// uploaded, a listing and a delete. The upload itself is what the link's command does - a post to Blob Management - and
/// is made here through its application service.
/// </summary>
public class FileTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private static readonly Guid EditorId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

    private readonly FileTools _tools;
    private readonly IBlobManagementUploadAppService _uploads;

    public FileTools_Tests()
    {
        GetRequiredService<IOptions<SiteMcpOptions>>().Value.BaseUrl = "https://site.example/";
        _tools = GetRequiredService<FileTools>();
        _uploads = GetRequiredService<IBlobManagementUploadAppService>();
    }

    [Fact]
    public async Task Should_List_The_Containers_With_Their_Limits()
    {
        var containers = await AsEditorAsync(() => _tools.ListFileContainersAsync());

        containers.Select(container => container.Name).ShouldBe(SiteFileContainerNames.All, ignoreOrder: true);
        containers.Single(container => container.Name == SiteFileContainerNames.Images).AllowedFileExtensions.ShouldContain(".png");
    }

    [Fact]
    public async Task An_Upload_Link_Carries_The_Command_And_The_File_Is_Kept_When_Confirmed()
    {
        var bytes = TestFiles.Pdf("mcp");

        var link = await AsEditorAsync(() => _tools.CreateUploadUrlAsync(SiteFileContainerNames.Attachments));

        link.Url.ShouldStartWith("https://site.example/api/blob-management/uploads/");
        link.Command.ShouldContain(link.Url);
        link.PowerShellCommand.ShouldContain("curl.exe");

        await AsEditorAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _uploads.UploadAsync(link.Url[(link.Url.LastIndexOf('/') + 1)..], new RemoteStreamContent(stream, "mcp.pdf", "application/pdf"));
        });

        var kept = await AsEditorAsync(() => _tools.ConfirmFileAsync(link.FileId));
        kept.Url.ShouldNotBeNull();
        kept.Url!.ShouldStartWith("/" + SiteFileUrl.RoutePrefix + "/" + SiteFileContainerNames.Attachments + "/");

        var page = await AsEditorAsync(() => _tools.ListFilesAsync(SiteFileContainerNames.Attachments, filter: "MCP"));
        page.TotalCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(link.FileId);

        (await AsEditorAsync(() => _tools.GetFileAsync(link.FileId))).Name.ShouldBe("mcp.pdf");

        await AsEditorAsync(() => _tools.DeleteFileAsync(link.FileId));
        (await AsEditorAsync(() => _tools.ListFilesAsync(SiteFileContainerNames.Attachments))).TotalCount.ShouldBe(0);
    }

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
            return await WithUnitOfWorkAsync(func);
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
}
