using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Themes;
using Dignite.Site.Themes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Site.Admin.Themes;

/// <summary>The theme as a zip, out and back in, directly and by one-time link (dev-docs/site-theme-import-export.md).</summary>
public class SiteThemeTransfer_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string Layout = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>";

    private readonly ISiteThemeAdminAppService _themeAppService;
    private readonly ISiteThemeTransferAdminAppService _transferAppService;
    private readonly ISiteThemeTransferTicketService _ticketService;

    public SiteThemeTransfer_Tests()
    {
        _themeAppService = GetRequiredService<ISiteThemeAdminAppService>();
        _transferAppService = GetRequiredService<ISiteThemeTransferAdminAppService>();
        _ticketService = GetRequiredService<ISiteThemeTransferTicketService>();
    }

    private Task<SiteThemeFileDto> SaveAsync(string path, string body) =>
        _themeAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = path, Body = body });

    private static byte[] Zip(IDictionary<string, string> entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, body) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                var bytes = Encoding.UTF8.GetBytes(body);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        return buffer.ToArray();
    }

    private Task<SiteThemeImportResultDto> ImportAsync(byte[] zip, bool replace = false) =>
        _transferAppService.ImportAsync(new ImportSiteThemeInput
        {
            File = new RemoteStreamContent(new MemoryStream(zip), "theme.zip", "application/zip"),
            Replace = replace
        });

    private static async Task<Dictionary<string, string>> ReadZipAsync(IRemoteStreamContent content)
    {
        await using var stream = content.GetStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            result[entry.FullName] = await reader.ReadToEndAsync();
        }

        return result;
    }

    private static string TicketOf(SiteThemeTransferLinkDto link) => link.Path[(link.Path.LastIndexOf('/') + 1)..];

    [Fact]
    public async Task Should_Export_The_Working_Copy_With_Its_Default_Layout()
    {
        await SaveAsync("Events", "<p>{{ page.display_name }}</p>");

        var export = await _transferAppService.ExportAsync(null);
        export.FileName.ShouldBe("theme-working-copy.zip");
        export.ContentType.ShouldBe("application/zip");

        var entries = await ReadZipAsync(export);
        entries["Events"].ShouldBe("<p>{{ page.display_name }}</p>");
        entries.Keys.ShouldContain("layouts/default");
        entries.Keys.ShouldContain("css/site.css");
        JsonNode.Parse(entries[SiteThemePackage.ManifestName])!["defaultLayout"]!.GetValue<string>().ShouldBe("layouts/default");
    }

    [Fact]
    public async Task An_Export_Imported_Again_Should_Change_Nothing()
    {
        await SaveAsync("Events", "<p>x</p>");
        await using var stream = (await _transferAppService.ExportAsync(null)).GetStream();
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        var result = await ImportAsync(copy.ToArray(), replace: true);

        result.Added.ShouldBeEmpty();
        result.Modified.ShouldBeEmpty();
        result.Deleted.ShouldBeEmpty();
        result.Unchanged.ShouldContain("Events");
        result.DefaultLayoutSet.ShouldBeTrue();
    }

    /// <summary>Merge: the package's files go in, the rest stays - a zip with only the stylesheet updates the stylesheet.</summary>
    [Fact]
    public async Task A_Merge_Should_Leave_The_Files_The_Package_Does_Not_Have()
    {
        await SaveAsync("Events", "<p>x</p>");

        var result = await ImportAsync(Zip(new Dictionary<string, string>
        {
            ["css/site.css"] = "body { margin: 0 }",
            ["News"] = "<p>{{ if }}</p>"
        }));

        result.Modified.ShouldBe(new[] { "css/site.css" });
        result.Added.ShouldBe(new[] { "News" });
        result.Deleted.ShouldBeEmpty();
        result.DefaultLayoutSet.ShouldBeFalse();
        result.SyntaxErrors.Keys.ShouldBe(new[] { "News" });

        var files = (await _themeAppService.GetFilesAsync()).Items.Select(f => f.Path).ToList();
        files.ShouldContain("Events");
        files.ShouldContain("News");
        (await _themeAppService.GetFileAsync("css/site.css")).Body.ShouldBe("body { margin: 0 }");
    }

    [Fact]
    public async Task A_Replace_Should_Make_The_Working_Copy_The_Package()
    {
        await SaveAsync("Events", "<p>x</p>");

        var result = await ImportAsync(Zip(new Dictionary<string, string>
        {
            ["layouts/main"] = Layout,
            [SiteThemePackage.ManifestName] = "{ \"defaultLayout\": \"layouts/main\" }"
        }), replace: true);

        result.Added.ShouldBe(new[] { "layouts/main" });
        result.Deleted.ShouldBe(new[] { "Events", "css/site.css", "layouts/default" });
        result.DefaultLayoutSet.ShouldBeTrue();
        (await _themeAppService.GetAsync()).DefaultLayout.ShouldBe("layouts/main");
        (await _themeAppService.GetFilesAsync()).Items.Where(f => f.Status != SiteThemeFileStatus.Deleted)
            .Select(f => f.Path).ShouldBe(new[] { "layouts/main" });
    }

    /// <summary>A zip made on a Mac carries hidden files and __MACOSX/; they are not theme files and are passed over.</summary>
    [Fact]
    public async Task Hidden_Files_Should_Be_Passed_Over()
    {
        var result = await ImportAsync(Zip(new Dictionary<string, string>
        {
            ["Events"] = "<p>x</p>",
            [".DS_Store"] = "x",
            ["css/.hidden"] = "x",
            ["__MACOSX/._Events"] = "x"
        }));

        result.Added.ShouldBe(new[] { "Events" });
    }

    [Fact]
    public async Task A_Package_With_Anything_Wrong_Should_Be_Refused_Whole()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => ImportAsync(Zip(new Dictionary<string, string>
        {
            ["Events"] = "<p>fine</p>",
            ["../outside"] = "x",
            ["post.html"] = "x",
            ["Bad Name"] = "x"
        })));

        exception.Code.ShouldBe(SiteErrorCodes.SiteThemePackageInvalid);
        var problems = (string)exception.Data["Problems"]!;
        problems.ShouldContain("../outside");
        problems.ShouldContain("post.html");
        problems.ShouldContain("Bad Name");
        (await _themeAppService.GetFilesAsync()).Items.Select(f => f.Path).ShouldNotContain("Events");
    }

    [Fact]
    public async Task A_File_That_Is_Not_UTF8_Or_Not_A_Zip_Should_Be_Refused()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var stream = archive.CreateEntry("css/site.css").Open();
            stream.Write(new byte[] { 0xFF, 0xFE, 0x00, 0xC3 }, 0, 4);
        }

        (await Should.ThrowAsync<BusinessException>(() => ImportAsync(buffer.ToArray())))
            .Data["Problems"]!.ToString()!.ShouldContain("not UTF-8");

        (await Should.ThrowAsync<BusinessException>(() => ImportAsync(Encoding.UTF8.GetBytes("not a zip"))))
            .Code.ShouldBe(SiteErrorCodes.SiteThemePackageInvalid);
    }

    [Fact]
    public async Task An_Import_Link_Should_Import_Once()
    {
        var link = await _transferAppService.CreateImportLinkAsync(new CreateSiteThemeImportLinkInput());
        link.Path.ShouldStartWith("api/site-admin/theme/upload/");
        link.ExpiresAt.ShouldBeLessThanOrEqualTo(DateTime.Now.AddMinutes(11));

        var zip = Zip(new Dictionary<string, string> { ["Events"] = "<p>x</p>" });
        var body = new MemoryStream(zip);
        var result = await _ticketService.UploadAsync(TicketOf(link), _ => body);

        result.Added.ShouldBe(new[] { "Events" });
        body.CanRead.ShouldBeTrue();

        (await Should.ThrowAsync<BusinessException>(() =>
                _ticketService.UploadAsync(TicketOf(link), _ => new MemoryStream(zip))))
            .Code.ShouldBe(SiteErrorCodes.SiteThemeTransferLinkInvalid);
    }

    [Fact]
    public async Task A_Download_Link_Should_Download_And_Not_Upload()
    {
        await SaveAsync("Events", "<p>x</p>");
        var link = await _transferAppService.CreateExportLinkAsync(new CreateSiteThemeExportLinkInput());
        link.Path.ShouldStartWith("api/site-admin/theme/download/");

        (await Should.ThrowAsync<BusinessException>(() =>
                _ticketService.UploadAsync(TicketOf(link), _ => new MemoryStream())))
            .Code.ShouldBe(SiteErrorCodes.SiteThemeTransferLinkInvalid);

        var link2 = await _transferAppService.CreateExportLinkAsync(new CreateSiteThemeExportLinkInput());
        var entries = await ReadZipAsync(await _ticketService.DownloadAsync(TicketOf(link2)));
        entries["Events"].ShouldBe("<p>x</p>");
    }

    [Fact]
    public async Task The_Mcp_Tools_Should_Give_A_Link_And_Its_Commands()
    {
        // No request here to take the address from: the host's configured one, as behind a gateway.
        GetRequiredService<Microsoft.Extensions.Options.IOptions<Dignite.Site.Mcp.Files.SiteMcpFileOptions>>().Value.UploadBaseUrl = "https://admin.example";
        var tools = GetRequiredService<SiteThemeTransferTools>();

        var upload = await tools.CreateUploadUrlAsync();
        upload.Url.ShouldContain("/api/site-admin/theme/upload/");
        upload.Command.ShouldContain("--data-binary \"@ZIP_PATH\"");
        upload.PowerShellCommand.ShouldContain("-InFile \"ZIP_PATH\"");

        var download = await tools.CreateDownloadUrlAsync();
        download.Url.ShouldContain("/api/site-admin/theme/download/");
        download.Command.ShouldContain("-o \"ZIP_PATH\"");

        await Should.ThrowAsync<AbpValidationException>(() => tools.CreateUploadUrlAsync("overwrite"));
    }
}
