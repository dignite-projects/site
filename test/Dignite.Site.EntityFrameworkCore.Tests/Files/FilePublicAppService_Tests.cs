using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Public.Files;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// The public read endpoint's logic: a live file in one of Site's containers, optionally resized; anything
/// else - another container, a deleted file, a name that does not exist - is simply not found.
/// </summary>
public class FilePublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFilePublicAppService _filePublicAppService;
    private readonly FileDescriptorManager _fileDescriptorManager;

    public FilePublicAppService_Tests()
    {
        _filePublicAppService = GetRequiredService<IFilePublicAppService>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
    }

    [Fact]
    public async Task Should_Serve_A_Stored_File_As_Stored()
    {
        var bytes = TestFiles.Pdf("served");
        var file = await UploadAsync(SiteFileContainerNames.Attachments, "served.pdf", bytes);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput());

        content.ContentType.ShouldBe("application/pdf");
        content.FileName.ShouldBe("served.pdf");
        (await ReadAllAsync(content.GetStream())).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Crop_An_Image_To_The_Requested_Size()
    {
        var file = await UploadAsync(SiteFileContainerNames.Images, "share.jpg", TestFiles.Jpeg(800, 600));

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 120, Height = 63 });

        var info = TestFiles.Identify(await ReadAllAsync(content.GetStream()));
        info.Width.ShouldBe(120);
        info.Height.ShouldBe(63);
        info.Format.ShouldBe(SKEncodedImageFormat.Jpeg);
        content.ContentType.ShouldBe("image/jpeg");
    }

    /// <summary>
    /// A downscale averages the pixels it drops (SitePublicApplicationModule's SkiaSharpResizerOptions) rather
    /// than ABP's nearest-neighbour default, which keeps one source pixel per target pixel: one-pixel black and
    /// white stripes shrunk 4x come out grey, where the default turns them all black or all white.
    /// </summary>
    [Fact]
    public async Task Should_Average_Pixels_When_Shrinking()
    {
        var file = await UploadAsync(SiteFileContainerNames.Images, "stripes.png", Stripes(400, 300));

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 100 });

        using var resized = SKBitmap.Decode(await ReadAllAsync(content.GetStream()));
        resized.Width.ShouldBe(100);
        for (var x = 10; x < 90; x += 7)
        {
            resized.GetPixel(x, 40).Red.ShouldBeInRange((byte)96, (byte)160);
        }
    }

    /// <summary>
    /// A requested size beyond the endpoint's bound is refused before the image is even looked at, with the
    /// Imaging package's code (the old "resize dimensions too large" code has no successor).
    /// </summary>
    [Fact]
    public async Task Should_Refuse_A_Requested_Size_Beyond_The_Bound()
    {
        var file = await UploadAsync(SiteFileContainerNames.Images, "big-ask.png", TestFiles.Png(32, 32));

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 5000 }));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageTooLarge);
    }

    /// <summary>
    /// SkiaSharp resizes JPEG, PNG and WebP; a GIF (an allowed upload type) is served as stored rather than
    /// failing the request.
    /// </summary>
    [Fact]
    public async Task Should_Serve_A_Picture_The_Provider_Cannot_Resize_As_Stored()
    {
        // A 1 x 1 GIF89a.
        var gif = new byte[]
        {
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B
        };
        var file = await UploadAsync(SiteFileContainerNames.Images, "dot.gif", gif);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 10, Height = 10 });

        content.ContentType.ShouldBe("image/gif");
        (await ReadAllAsync(content.GetStream())).ShouldBe(gif);
    }

    /// <summary>A size asked of a file that is not an image is ignored, not an error.</summary>
    [Fact]
    public async Task Should_Serve_A_Non_Image_As_Is_When_A_Size_Is_Asked()
    {
        var bytes = TestFiles.Pdf("not resizable");
        var file = await UploadAsync(SiteFileContainerNames.Attachments, "doc.pdf", bytes);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 100 });

        (await ReadAllAsync(content.GetStream())).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Not_Serve_A_Deleted_File()
    {
        var file = await UploadAsync(SiteFileContainerNames.Attachments, "deleted.pdf", TestFiles.Pdf("deleted"));
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(file));

        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput()));
    }

    /// <summary>
    /// Only Site's containers are served - whatever else the host registered, and even a name that exists
    /// in one of Site's containers asked under another container's name.
    /// </summary>
    [Fact]
    public async Task Should_Not_Serve_Outside_Sites_Containers()
    {
        var file = await UploadAsync(SiteFileContainerNames.Attachments, "mine.pdf", TestFiles.Pdf("mine"));

        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _filePublicAppService.GetAsync("invoices", file.BlobName, new GetFileInput()));
        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _filePublicAppService.GetAsync(SiteFileContainerNames.Images, file.BlobName, new GetFileInput()));
        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _filePublicAppService.GetAsync(SiteFileContainerNames.Attachments, "does-not-exist", new GetFileInput()));
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string fileName, byte[] bytes)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, fileName, stream);
        });
    }

    /// <summary>
    /// A PNG of alternating one-pixel dark and light columns. The noise in each shade keeps PNG from compressing
    /// it below the upload decode guard's 100 pixels per stored byte.
    /// </summary>
    private static byte[] Stripes(int width, int height)
    {
        var random = new Random(42);
        using var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var v = (byte)(x % 2 == 0 ? random.Next(0, 24) : random.Next(232, 256));
            bitmap.SetPixel(x, y, new SKColor(v, v, v));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
