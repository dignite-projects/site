using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Site.Admin.Files;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using SkiaSharp;
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
/// from a right one - <c>FileDescriptorAuthorizationHandler_Tests</c> covers what the names do. The size and
/// type checks, in contrast, run for real through <see cref="FileDescriptorManager"/> and the containers' BLOB
/// pipeline contributors.
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
    public void Container_Has_A_Size_Limit(string containerName, int expectedMaxSizeInMb)
    {
        _configurationProvider.Get(containerName).GetMaxSizeContributorConfiguration().MaxSizeInBytes
            .ShouldBe(expectedMaxSizeInMb * 1024L * 1024L);
    }

    /// <summary>
    /// Exact raster types, never <c>image/*</c> - the wildcard would let <c>image/svg+xml</c> in.
    /// </summary>
    [Theory]
    [InlineData(SiteFileContainerNames.Default)]
    [InlineData(SiteFileContainerNames.Images)]
    public void Container_Allows_Exact_Content_Types_Only(string containerName)
    {
        var allowed = _configurationProvider.Get(containerName).GetAllowedContentTypesContributorConfiguration().AllowedContentTypes;

        allowed.ShouldContain("image/png");
        allowed.ShouldNotContain("image/*");
        allowed.ShouldNotContain("image/svg+xml");
        allowed.ShouldNotContain("text/html");
    }

    [Fact]
    public async Task Upload_Larger_Than_The_Container_Limit_Is_Rejected()
    {
        var bytes = new byte[10 * 1024 * 1024 + 1];

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            UploadAsync(SiteFileContainerNames.Images, "huge.png", bytes));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
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

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
    }

    /// <summary>The type comes from the bytes: text named <c>.png</c> is not let in as an image.</summary>
    [Fact]
    public async Task Content_That_Contradicts_Its_Extension_Is_Rejected()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            UploadAsync(SiteFileContainerNames.Images, "photo.png", TestFiles.Text("not a picture")));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
    }

    /// <summary>
    /// Within one kind the content wins: a PNG named <c>.jpg</c> is not a disguised file, just a misnamed
    /// picture, and is recorded as what it is.
    /// </summary>
    [Fact]
    public async Task Picture_Named_With_Another_Picture_Extension_Is_Stored_As_What_It_Is()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "photo.jpg", TestFiles.Png(16, 16));

        descriptor.MimeType.ShouldBe("image/png");
        descriptor.Name.ShouldBe("photo.jpg");
        // The blob's extension comes from the content, not from the uploader's name.
        descriptor.BlobName.ShouldMatch(@"^[0-9a-f]{32}\.png$");
    }

    [Fact]
    public async Task Uploaded_Picture_Blob_Name_Is_32_Hex_Digits_Plus_The_Canonical_Extension()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "picture.png", TestFiles.Png(16, 16));

        descriptor.BlobName.ShouldMatch(@"^[0-9a-f]{32}\.png$");
        descriptor.MimeType.ShouldBe("image/png");
    }

    [Fact]
    public async Task Empty_File_Takes_Its_Extensions_Type()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Default, "empty.txt", []);

        descriptor.MimeType.ShouldBe("text/plain");
        descriptor.Size.ShouldBe(0);
        descriptor.BlobName.ShouldMatch(@"^[0-9a-f]{32}\.txt$");
    }

    [Fact]
    public async Task Compound_File_Named_Doc_Is_Accepted_As_Msword()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Default, "legacy.doc", TestFiles.CompoundFile());

        descriptor.MimeType.ShouldBe("application/msword");
        descriptor.BlobName.ShouldMatch(@"^[0-9a-f]{32}\.doc$");
    }

    [Fact]
    public async Task Image_Wider_Than_The_Images_Container_Limit_Is_Scaled_Down_On_Upload()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "banner.jpg", TestFiles.Jpeg(3000, 1000));

        var stored = await ReadAsync(descriptor);
        var info = TestFiles.Identify(stored);

        info.Width.ShouldBe(1920);
        info.Height.ShouldBe(640); // aspect ratio kept
        info.Format.ShouldBe(SKEncodedImageFormat.Jpeg);
        descriptor.MimeType.ShouldBe("image/jpeg");
        // The recorded size is what is stored, not what was uploaded.
        descriptor.Size.ShouldBe(stored.Length);
    }

    [Fact]
    public async Task Image_Contributors_Leave_Non_Image_Files_Untouched()
    {
        var bytes = TestFiles.Pdf();

        var descriptor = await UploadAsync(SiteFileContainerNames.Default, "report.pdf", bytes);

        (await ReadAsync(descriptor)).ShouldBe(bytes);
        descriptor.MimeType.ShouldBe("application/pdf");
        descriptor.Size.ShouldBe(bytes.Length);
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string fileName, byte[] bytes)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, fileName, stream);
        });
    }

    private Task<byte[]> ReadAsync(FileDescriptor descriptor)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _fileDescriptorManager.GetStreamOrNullAsync(descriptor);
            stream.ShouldNotBeNull();
            using var memory = new MemoryStream();
            await stream!.CopyToAsync(memory);
            return memory.ToArray();
        });
    }
}
