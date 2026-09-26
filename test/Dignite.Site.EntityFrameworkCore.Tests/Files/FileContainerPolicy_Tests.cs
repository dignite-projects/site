using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.FileExplorer.Files;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.Content;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// Each <see cref="SiteFileContainerNames"/> container's policy, as SiteAdminApplicationModule owns it.
/// The test module configures only the provider, same as a real host - so these read the module's own
/// policy, and the provider assertion proves the host's <c>Containers.Configure</c> call merged onto it
/// rather than replacing it.
/// <para>
/// The permission assertions are plain configuration reads, not authorization-behaviour tests:
/// <c>SiteTestBaseModule.AddAlwaysAllowAuthorization</c> makes every permission check in this suite
/// succeed, so a live call could not tell a wrong permission name from a right one. Whether the names
/// actually gate FileDescriptorAppService is FileExplorer's own mechanism, covered by that repo's
/// <c>FileDescriptorAuthorizationHandler_Tests</c>. The type checks, in contrast, are exercised for real
/// through <see cref="FileDescriptorManager"/>.
/// </para>
/// </summary>
public class FileContainerPolicy_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IBlobContainerConfigurationProvider _configurationProvider;
    private readonly FileDescriptorManager _fileDescriptorManager;

    public FileContainerPolicy_Tests()
    {
        _configurationProvider = GetRequiredService<IBlobContainerConfigurationProvider>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
    }

    [Theory]
    [InlineData(SiteFileContainerNames.Default)]
    [InlineData(SiteFileContainerNames.Images)]
    public void Container_Rides_On_Contents_Permissions_And_Stays_Publicly_Readable(string containerName)
    {
        var configuration = _configurationProvider.Get(containerName);
        var authorization = configuration.GetAuthorizationConfiguration();

        authorization.CreateFilePermissionName.ShouldBe(SiteAdminPermissions.Contents.Create);
        // Set explicitly - FileExplorer's default for unset is "creator only", which would stop one editor
        // from replacing or removing another editor's file.
        authorization.UpdateFilePermissionName.ShouldBe(SiteAdminPermissions.Contents.Update);
        authorization.DeleteFilePermissionName.ShouldBe(SiteAdminPermissions.Contents.Delete);
        // Set explicitly - FileExplorer's default for unset is "nobody may create a directory".
        authorization.CreateDirectoryPermissionName.ShouldBe(SiteAdminPermissions.Contents.Create);

        // Unset, not locked down: a published content's files must load for anonymous site visitors
        // (FileDescriptorAuthorizationHandler's own default for unset is "everyone may read").
        authorization.GetFilePermissionName.ShouldBeNull();

        configuration.ProviderType.ShouldBe(typeof(FileSystemBlobProvider));
    }

    [Theory]
    [InlineData(SiteFileContainerNames.Default, 20)]
    [InlineData(SiteFileContainerNames.Images, 10)]
    public void Container_Has_A_Size_Limit(string containerName, int expectedMaxFileSizeInMb)
    {
        _configurationProvider.Get(containerName).GetFileSizeLimitConfiguration().MaxFileSize
            .ShouldBe(expectedMaxFileSizeInMb);
    }

    [Theory]
    [InlineData(SiteFileContainerNames.Images, "photo.jpg")]
    [InlineData(SiteFileContainerNames.Images, "photo.WEBP")]
    [InlineData(SiteFileContainerNames.Default, "photo.png")]
    [InlineData(SiteFileContainerNames.Default, "report.pdf")]
    public async Task Allowed_File_Type_Is_Stored(string containerName, string fileName)
    {
        var descriptor = await UploadAsync(containerName, fileName);

        descriptor.ContainerName.ShouldBe(containerName);
    }

    [Theory]
    // Rendered and executed by a browser on the site's own origin - stored XSS, since reads are public.
    [InlineData(SiteFileContainerNames.Images, "logo.svg")]
    [InlineData(SiteFileContainerNames.Default, "logo.svg")]
    [InlineData(SiteFileContainerNames.Default, "page.html")]
    [InlineData(SiteFileContainerNames.Default, "script.js")]
    // Images only: a document is fine in site-files but not here.
    [InlineData(SiteFileContainerNames.Images, "report.pdf")]
    public async Task Disallowed_File_Type_Is_Rejected(string containerName, string fileName)
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => UploadAsync(containerName, fileName));

        exception.Code.ShouldBe(FileErrorCodes.Files.InvalidImageType);
    }

    [Fact]
    public async Task Image_Wider_Than_The_Images_Container_Limit_Is_Scaled_Down_On_Upload()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "banner.jpg", CreateJpeg(3000, 1000));

        var stored = await WithUnitOfWorkAsync(() => _fileDescriptorManager.GetStreamOrNullAsync(
            SiteFileContainerNames.Images, descriptor.BlobName));
        var info = await Image.IdentifyAsync(stored!);

        info.Width.ShouldBe(1920);
        info.Height.ShouldBe(640); // aspect ratio kept
    }

    [Fact]
    public async Task Resize_Handler_Leaves_Non_Image_Files_Untouched()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4 not really a pdf, but not an image either");

        var descriptor = await UploadAsync(SiteFileContainerNames.Default, "report.pdf", bytes);

        var stored = await WithUnitOfWorkAsync(() => _fileDescriptorManager.GetStreamOrNullAsync(
            SiteFileContainerNames.Default, descriptor.BlobName));
        using var readBack = new MemoryStream();
        await stored!.CopyToAsync(readBack);
        readBack.ToArray().ShouldBe(bytes);
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string fileName, byte[]? bytes = null)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes ?? Encoding.UTF8.GetBytes("policy test"));
            var content = new RemoteStreamContent(stream, fileName, "application/octet-stream");

            return await _fileDescriptorManager.CreateAsync(
                containerName, content, cellName: null, directoryId: null, entityId: null);
        });
    }

    /// <summary>
    /// Noise rather than a flat colour: a flat image compresses so well that it trips ImageResizeHandler's
    /// decompression-ratio guard (pixels per stored byte) before any resizing happens.
    /// </summary>
    private static byte[] CreateJpeg(int width, int height)
    {
        var random = new Random(42);
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                foreach (ref var pixel in rows.GetRowSpan(y))
                {
                    pixel = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                }
            }
        });

        using var output = new MemoryStream();
        image.SaveAsJpeg(output);
        return output.ToArray();
    }
}
