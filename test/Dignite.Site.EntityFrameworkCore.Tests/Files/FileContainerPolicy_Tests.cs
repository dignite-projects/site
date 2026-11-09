using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    [InlineData(SiteFileContainerNames.Attachments)]
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

    /// <summary>
    /// The theme's binary files are design, not content: they are written with the theme's edit permission, and
    /// read by anyone, like the other containers.
    /// </summary>
    [Fact]
    public void Theme_Assets_Container_Rides_On_The_Themes_Edit_Permission_And_Stays_Publicly_Readable()
    {
        var configuration = _configurationProvider.Get(SiteFileContainerNames.ThemeAssets);
        var authorization = configuration.GetAuthorizationConfiguration();

        authorization.CreateFilePermissionName.ShouldBe(SiteAdminPermissions.Themes.Edit);
        authorization.UpdateFilePermissionName.ShouldBe(SiteAdminPermissions.Themes.Edit);
        authorization.DeleteFilePermissionName.ShouldBe(SiteAdminPermissions.Themes.Edit);
        authorization.CreateDirectoryPermissionName.ShouldBe(SiteAdminPermissions.Themes.Edit);
        authorization.GetFilePermissionName.ShouldBeNull();
        configuration.ProviderType.ShouldBe(typeof(FileSystemBlobProvider));
        configuration.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(5L * 1024 * 1024);
    }

    /// <summary>
    /// What a client is told a container does to an image is read from the container's own configuration: images
    /// are shrunk to fit but not re-encoded, the other containers store what they are given.
    /// </summary>
    [Fact]
    public async Task The_Container_Configuration_Reports_What_It_Does_To_An_Image()
    {
        var fileAppService = GetRequiredService<Dignite.Site.Admin.Files.IFileAdminAppService>();

        var images = await fileAppService.GetContainerConfigurationAsync(SiteFileContainerNames.Images);
        images.ImageMaxWidth.ShouldBe(4096);
        images.ImageMaxHeight.ShouldBe(4096);
        images.CompressesImages.ShouldBeFalse();

        foreach (var name in new[] { SiteFileContainerNames.Attachments, SiteFileContainerNames.ThemeAssets })
        {
            var configuration = await fileAppService.GetContainerConfigurationAsync(name);
            configuration.ImageMaxWidth.ShouldBeNull();
            configuration.ImageMaxHeight.ShouldBeNull();
            configuration.CompressesImages.ShouldBeFalse();
        }
    }

    /// <summary>An icon is stored as it is: not resized, not re-encoded, and no picture or document gets in beside it.</summary>
    [Fact]
    public async Task Theme_Assets_Container_Stores_An_Icon_As_Uploaded()
    {
        var ico = TestFiles.Ico();

        var descriptor = await UploadAsync(SiteFileContainerNames.ThemeAssets, "favicon.ico", ico);

        descriptor.MimeType.ShouldBe("image/x-icon");
        (await ReadAsync(descriptor)).ShouldBe(ico);
    }

    [Theory]
    [InlineData("photo.png")]
    [InlineData("report.pdf")]
    [InlineData("logo.svg")]
    [InlineData("page.html")]
    public async Task Theme_Assets_Container_Takes_Nothing_But_What_It_Lists(string fileName)
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => UploadAsync(SiteFileContainerNames.ThemeAssets, fileName, TestFiles.For(fileName)));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
    }

    [Theory]
    [InlineData(SiteFileContainerNames.Attachments, 20)]
    [InlineData(SiteFileContainerNames.Images, 25)]
    public void Container_Has_A_Size_Limit(string containerName, int expectedMaxSizeInMb)
    {
        _configurationProvider.Get(containerName).GetMaxSizeContributorConfiguration().MaxSizeInBytes
            .ShouldBe(expectedMaxSizeInMb * 1024L * 1024L);
    }

    /// <summary>
    /// Exact raster types, never <c>image/*</c> - the wildcard would let <c>image/svg+xml</c> in.
    /// </summary>
    [Theory]
    [InlineData(SiteFileContainerNames.Attachments)]
    [InlineData(SiteFileContainerNames.Images)]
    public void Container_Allows_Exact_Content_Types_Only(string containerName)
    {
        var allowed = _configurationProvider.Get(containerName).GetAllowedContentTypesContributorConfiguration().AllowedContentTypes;

        if (containerName == SiteFileContainerNames.Images)
        {
            allowed.ShouldContain("image/png");
            allowed.ShouldNotContain("application/pdf");
        }
        else
        {
            allowed.ShouldContain("application/pdf");
            allowed.ShouldNotContain("image/png");
        }

        allowed.ShouldNotContain("image/*");
        allowed.ShouldNotContain("image/svg+xml");
        allowed.ShouldNotContain("text/html");
    }

    [Fact]
    public async Task Upload_Larger_Than_The_Container_Limit_Is_Rejected()
    {
        var bytes = new byte[25 * 1024 * 1024 + 1];

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            UploadAsync(SiteFileContainerNames.Images, "huge.png", bytes));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
    }

    [Theory]
    [InlineData(SiteFileContainerNames.Images, "photo.jpg")]
    [InlineData(SiteFileContainerNames.Images, "photo.WEBP")]
    [InlineData(SiteFileContainerNames.Attachments, "report.pdf")]
    public async Task Allowed_File_Type_Is_Stored(string containerName, string fileName)
    {
        var descriptor = await UploadAsync(containerName, fileName, TestFiles.For(fileName));

        descriptor.ContainerName.ShouldBe(containerName);
    }

    [Theory]
    // Rendered and executed by a browser on the site's own origin - stored XSS, since reads are public.
    [InlineData(SiteFileContainerNames.Images, "logo.svg")]
    [InlineData(SiteFileContainerNames.Attachments, "logo.svg")]
    [InlineData(SiteFileContainerNames.Attachments, "page.html")]
    [InlineData(SiteFileContainerNames.Attachments, "script.js")]
    // Documents only: a picture belongs in images.
    [InlineData(SiteFileContainerNames.Attachments, "photo.png")]
    [InlineData(SiteFileContainerNames.Attachments, "photo.jpg")]
    // Images only: a document is fine in attachments but not here.
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
        var descriptor = await UploadAsync(SiteFileContainerNames.Attachments, "empty.txt", []);

        descriptor.MimeType.ShouldBe("text/plain");
        descriptor.Size.ShouldBe(0);
        descriptor.BlobName.ShouldMatch(@"^[0-9a-f]{32}\.txt$");
    }

    [Fact]
    public async Task Compound_File_Named_Doc_Is_Accepted_As_Msword()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Attachments, "legacy.doc", TestFiles.CompoundFile());

        descriptor.MimeType.ShouldBe("application/msword");
        descriptor.BlobName.ShouldMatch(@"^[0-9a-f]{32}\.doc$");
    }

    [Fact]
    public async Task Image_Wider_Than_The_Images_Container_Limit_Is_Scaled_Down_On_Upload()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "banner.jpg", TestFiles.Jpeg(5120, 1280));

        var stored = await ReadAsync(descriptor);
        var info = TestFiles.Identify(stored);

        info.Width.ShouldBe(4096);
        info.Height.ShouldBe(1024); // aspect ratio kept
        info.Format.ShouldBe(SKEncodedImageFormat.Jpeg);
        descriptor.MimeType.ShouldBe("image/jpeg");
        // The recorded size is what is stored, not what was uploaded.
        descriptor.Size.ShouldBe(stored.Length);
    }

    /// <summary>
    /// A phone stores a portrait photo sideways and records the turn in its EXIF orientation. Shrinking the photo
    /// re-encodes it without the EXIF, so it is turned upright first: the stored picture is upright, fits the box with
    /// its long side, and has no orientation left that a browser would apply a second time.
    /// </summary>
    [Theory]
    // A quarter turn clockwise to display - the red left half of the stored picture comes out on top.
    [InlineData(6, true)]
    // A quarter turn anticlockwise - the red comes out at the bottom.
    [InlineData(8, false)]
    public async Task Sideways_Photo_Larger_Than_The_Images_Container_Limit_Is_Stored_Upright(byte orientation, bool redOnTop)
    {
        var upload = TestFiles.WithExifOrientation(TestFiles.RedLeftBlueRightJpeg(5120, 2560), orientation);

        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "portrait.jpg", upload);

        var stored = await ReadAsync(descriptor);
        using var data = SKData.CreateCopy(stored);
        using var codec = SKCodec.Create(data);
        codec.ShouldNotBeNull();
        codec.EncodedFormat.ShouldBe(SKEncodedImageFormat.Jpeg);
        codec.EncodedOrigin.ShouldBe(SKEncodedOrigin.TopLeft);
        codec.Info.Width.ShouldBe(2048);
        codec.Info.Height.ShouldBe(4096);

        using var picture = SKBitmap.Decode(codec);
        var top = TestFiles.AverageColor(picture, new SKRectI(768, 768, 1280, 1280));
        var bottom = TestFiles.AverageColor(picture, new SKRectI(768, 2816, 1280, 3328));
        var (red, blue) = redOnTop ? (top, bottom) : (bottom, top);
        red.Red.ShouldBeGreaterThan(180);
        red.Blue.ShouldBeLessThan(80);
        blue.Blue.ShouldBeGreaterThan(180);
        blue.Red.ShouldBeLessThan(80);
        descriptor.Size.ShouldBe(stored.Length);
    }

    /// <summary>
    /// A picture that fits the box is the original, byte for byte: every image the site shows is rendered from it,
    /// so a lossy re-encode on upload would only be a loss the rendering cannot undo.
    /// </summary>
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("drawing.png")]
    [InlineData("picture.webp")]
    public async Task Image_That_Fits_The_Images_Container_Is_Stored_As_Uploaded(string fileName)
    {
        var bytes = Path.GetExtension(fileName) switch
        {
            ".jpg" => TestFiles.Jpeg(1200, 800),
            ".png" => TestFiles.Png(1200, 800),
            _ => TestFiles.Webp(1200, 800)
        };

        var descriptor = await UploadAsync(SiteFileContainerNames.Images, fileName, bytes);

        (await ReadAsync(descriptor)).ShouldBe(bytes);
        descriptor.Size.ShouldBe(bytes.Length);
    }

    /// <summary>
    /// A JPEG exported from Photoshop can carry megabytes of Extended XMP (<c>photoshop:DocumentAncestors</c>), the
    /// Photoshop image resources and comments. They are dropped, and every other byte stays: EXIF, the main XMP
    /// packet, the Adobe segment and the image itself, so the stored file is the upload less those segments.
    /// Above the 10 MB a picture could be before, and accepted: the limit is on what is sent.
    /// </summary>
    [Fact]
    public async Task Jpeg_Metadata_A_Design_Tool_Piles_Up_Is_Dropped_On_Upload()
    {
        var jpeg = TestFiles.Jpeg(640, 400);
        var exif = JpegSegment(0xE1, Concat("Exif\0\0"u8.ToArray(), new byte[]
        {
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, 0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        }));
        var xmp = JpegSegment(0xE1, Concat("http://ns.adobe.com/xap/1.0/\0"u8.ToArray(),
            "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><xmpNote:HasExtendedXMP>0123</xmpNote:HasExtendedXMP></x:xmpmeta>"u8.ToArray()));
        var adobe = JpegSegment(0xEE, "Adobe\0\x64\0\0\0\0\x01"u8.ToArray());
        var photoshop = JpegSegment(0xED, Concat("Photoshop 3.0\08BIM"u8.ToArray(), new byte[8000]));
        var comment = JpegSegment(0xFE, "Saved by a design tool"u8.ToArray());

        var segments = new List<byte[]> { exif, xmp };
        var ancestors = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("<rdf:li>0000A6C7815905497C2762FB3073AC1B</rdf:li> ", 1300)));
        for (var i = 0; i < 200; i++)
        {
            segments.Add(JpegSegment(0xE1, Concat("http://ns.adobe.com/xmp/extension/\0"u8.ToArray(), ancestors[..65000])));
        }

        segments.AddRange(new[] { photoshop, comment, adobe });
        var upload = Concat(jpeg[..2], Concat(segments.ToArray()), jpeg[2..]);
        upload.Length.ShouldBeGreaterThan(12 * 1024 * 1024);

        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "banner.jpg", upload);

        var stored = await ReadAsync(descriptor);
        stored.ShouldBe(Concat(jpeg[..2], exif, xmp, adobe, jpeg[2..]));
        descriptor.Size.ShouldBe(stored.Length);
        descriptor.MimeType.ShouldBe("image/jpeg");
    }

    /// <summary>The text chunks of a PNG - where Photoshop writes its XMP - go; every other chunk stays.</summary>
    [Fact]
    public async Task Png_Text_Chunks_Are_Dropped_On_Upload()
    {
        var png = TestFiles.Png(320, 200);
        var ihdrEnd = 8 + 12 + 13;
        var text = PngChunk("tEXt", "Software\0Design Tool"u8.ToArray());
        var itxt = PngChunk("iTXt", Concat("XML:com.adobe.xmp\0\0\0\0\0"u8.ToArray(), new byte[300_000]));
        var time = PngChunk("tIME", new byte[] { 0x07, 0xEA, 11, 9, 12, 0, 0 });

        var upload = Concat(png[..ihdrEnd], text, itxt, time, png[ihdrEnd..]);

        var descriptor = await UploadAsync(SiteFileContainerNames.Images, "drawing.png", upload);

        (await ReadAsync(descriptor)).ShouldBe(png);
    }

    [Fact]
    public async Task Image_Contributors_Leave_Non_Image_Files_Untouched()
    {
        var bytes = TestFiles.Pdf();

        var descriptor = await UploadAsync(SiteFileContainerNames.Attachments, "report.pdf", bytes);

        (await ReadAsync(descriptor)).ShouldBe(bytes);
        descriptor.MimeType.ShouldBe("application/pdf");
        descriptor.Size.ShouldBe(bytes.Length);
    }

    private static byte[] JpegSegment(byte marker, byte[] body)
    {
        var length = body.Length + 2;
        return Concat(new byte[] { 0xFF, marker, (byte)(length >> 8), (byte)length }, body);
    }

    private static byte[] PngChunk(string type, byte[] data)
    {
        var typeAndData = Concat(Encoding.ASCII.GetBytes(type), data);
        var crc = Crc32(typeAndData);
        return Concat(
            new[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length },
            typeAndData,
            new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
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
