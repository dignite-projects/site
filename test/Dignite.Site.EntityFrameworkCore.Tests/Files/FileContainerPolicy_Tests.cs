using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.Site.Admin.Files;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using SixLabors.ImageSharp;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// Each <see cref="SiteFileContainerNames"/> container's policy, as SiteAdminApplicationModule owns it.
/// The test module configures only the provider, same as a real host - so these read the module's own
/// policy, and the provider assertion proves the host's <c>Containers.Configure</c> call merged onto it
/// rather than replacing it.
/// <para>
/// The permission assertions are configuration reads: <c>SiteTestBaseModule.AddAlwaysAllowAuthorization</c>
/// makes every permission check in this suite succeed, so a live call could not tell a wrong permission name
/// from a right one - <c>FileDescriptorAuthorizationHandler_Tests</c> covers what the names do. The type
/// checks, in contrast, run for real through <see cref="FileDescriptorManager"/> and <c>IFileStorer</c>.
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
        // Set explicitly - the default for unset is "creator only", which would stop one editor from
        // replacing or removing another editor's file.
        authorization.UpdateFilePermissionName.ShouldBe(SiteAdminPermissions.Contents.Update);
        authorization.DeleteFilePermissionName.ShouldBe(SiteAdminPermissions.Contents.Delete);
        // Set explicitly - the default for unset is "nobody may create a directory".
        authorization.CreateDirectoryPermissionName.ShouldBe(SiteAdminPermissions.Contents.Create);

        // Unset, not locked down: a published content's files load for anonymous site visitors.
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
        var descriptor = await UploadAsync(containerName, fileName, TestFiles.For(fileName));

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
        var exception = await Should.ThrowAsync<BusinessException>(() => UploadAsync(containerName, fileName, TestFiles.For(fileName)));

        exception.Code.ShouldBe(FileErrorCodes.Files.InvalidImageType);
    }

    /// <summary>The type comes from the bytes: text named <c>.png</c> is not let in as an image.</summary>
    [Fact]
    public async Task Content_That_Contradicts_Its_Extension_Is_Rejected()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            UploadAsync(SiteFileContainerNames.Images, "photo.png", TestFiles.Text("not a picture")));

        exception.Code.ShouldBe(FileErrorCodes.Files.ContentTypeMismatch);
    }

    [Fact]
    public async Task Image_Wider_Than_The_Images_Container_Limit_Is_Scaled_Down_On_Upload()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "banner.jpg", TestFiles.Jpeg(3000, 1000));

        var stored = await WithUnitOfWorkAsync(() => _fileDescriptorManager.GetStreamOrNullAsync(descriptor));
        var info = await Image.IdentifyAsync(stored!);

        info.Width.ShouldBe(1920);
        info.Height.ShouldBe(640); // aspect ratio kept
        descriptor.MimeType.ShouldBe("image/jpeg");
    }

    [Fact]
    public async Task Resize_Handler_Leaves_Non_Image_Files_Untouched()
    {
        var bytes = TestFiles.Pdf();

        var descriptor = await UploadAsync(SiteFileContainerNames.Default, "report.pdf", bytes);

        var stored = await WithUnitOfWorkAsync(() => _fileDescriptorManager.GetStreamOrNullAsync(descriptor));
        using var readBack = new MemoryStream();
        await stored!.CopyToAsync(readBack);
        readBack.ToArray().ShouldBe(bytes);
        descriptor.MimeType.ShouldBe("application/pdf");
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string fileName, byte[] bytes)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, fileName, stream);
        });
    }
}
