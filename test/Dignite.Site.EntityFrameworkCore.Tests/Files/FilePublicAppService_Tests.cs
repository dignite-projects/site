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
    /// A downscale averages the pixels it drops (SiteImageRenderingOptions.Sampling) rather than SkiaSharp's
    /// nearest-neighbour default, which keeps one source pixel per target pixel: one-pixel black and
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
    /// A JPEG asked for by a caller that takes WebP is served as WebP at its own size, under a name that says so.
    /// </summary>
    [Fact]
    public async Task Should_Serve_A_Jpeg_As_Webp_To_A_Caller_That_Takes_It()
    {
        var jpeg = Photo(640, 400);
        var file = await UploadAsync(SiteFileContainerNames.Images, "photo.jpg", jpeg);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Format = SiteFileFormats.Webp });

        var webp = await ReadAllAsync(content.GetStream());
        var info = TestFiles.Identify(webp);
        info.Format.ShouldBe(SKEncodedImageFormat.Webp);
        info.Width.ShouldBe(640);
        info.Height.ShouldBe(400);
        webp.Length.ShouldBeLessThan(jpeg.Length);
        content.ContentType.ShouldBe("image/webp");
        content.FileName.ShouldBe("photo.webp");
    }

    /// <summary>
    /// Asked at its stored size, a picture whose WebP would be no smaller - noise, which no encoder shrinks - is
    /// served as stored rather than made larger.
    /// </summary>
    [Fact]
    public async Task Should_Serve_A_Picture_As_Stored_When_Its_Webp_Is_No_Smaller()
    {
        var jpeg = TestFiles.Jpeg(320, 200);
        var file = await UploadAsync(SiteFileContainerNames.Images, "noise.jpg", jpeg);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Format = SiteFileFormats.Webp });

        content.ContentType.ShouldBe("image/jpeg");
        content.FileName.ShouldBe("noise.jpg");
        (await ReadAllAsync(content.GetStream())).ShouldBe(jpeg);
    }

    /// <summary>
    /// A PNG goes to lossless WebP: every pixel and the transparency come back exactly, where lossy WebP would blur
    /// the edges of text and lines.
    /// </summary>
    [Fact]
    public async Task Should_Serve_A_Png_As_Lossless_Webp()
    {
        var png = TransparentDrawing(600, 300);
        var file = await UploadAsync(SiteFileContainerNames.Images, "logo.png", png);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Format = SiteFileFormats.Webp });

        var webp = await ReadAllAsync(content.GetStream());
        content.ContentType.ShouldBe("image/webp");
        TestFiles.Identify(webp).Format.ShouldBe(SKEncodedImageFormat.Webp);
        webp.Length.ShouldBeLessThan(png.Length);

        using var expected = SKBitmap.Decode(png);
        using var actual = SKBitmap.Decode(webp);
        actual.Width.ShouldBe(expected.Width);
        actual.Height.ShouldBe(expected.Height);
        for (var y = 0; y < expected.Height; y += 3)
        for (var x = 0; x < expected.Width; x += 3)
        {
            var e = expected.GetPixel(x, y);
            var a = actual.GetPixel(x, y);
            a.Alpha.ShouldBe(e.Alpha);
            if (e.Alpha == 255)
            {
                a.ShouldBe(e, $"pixel ({x}, {y})");
            }
        }
    }

    [Fact]
    public async Task Should_Crop_To_Webp_When_The_Caller_Takes_It()
    {
        var file = await UploadAsync(SiteFileContainerNames.Images, "share.jpg", TestFiles.Jpeg(800, 600));

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 120, Height = 63, Format = SiteFileFormats.Webp });

        var info = TestFiles.Identify(await ReadAllAsync(content.GetStream()));
        info.Width.ShouldBe(120);
        info.Height.ShouldBe(63);
        info.Format.ShouldBe(SKEncodedImageFormat.Webp);
    }

    /// <summary>
    /// A camera stores a portrait photo sideways and says in its EXIF orientation how to turn it. A browser turns
    /// the stored file; a rendered image has no EXIF, so it is turned before it is encoded.
    /// </summary>
    [Fact]
    public async Task Should_Turn_A_Photo_Upright_When_Rendering_It()
    {
        // Stored 400 x 200, red on the left and blue on the right; orientation 6 turns it a quarter clockwise, which
        // puts the red on top of a 200 x 400 picture.
        var file = await UploadAsync(SiteFileContainerNames.Images, "portrait.jpg", SidewaysJpeg());

        foreach (var input in new[] { new GetFileInput { Width = 100 }, new GetFileInput { Format = SiteFileFormats.Webp } })
        {
            using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, input);

            content.ContentType.ShouldBe(input.Format == SiteFileFormats.Webp ? "image/webp" : "image/jpeg");
            using var upright = SKBitmap.Decode(await ReadAllAsync(content.GetStream()));
            upright.Height.ShouldBe(upright.Width * 2);
            var top = upright.GetPixel(upright.Width / 2, upright.Height / 4);
            var bottom = upright.GetPixel(upright.Width / 2, upright.Height * 3 / 4);
            top.Red.ShouldBeGreaterThan((byte)200);
            top.Blue.ShouldBeLessThan((byte)60);
            bottom.Blue.ShouldBeGreaterThan((byte)200);
            bottom.Red.ShouldBeLessThan((byte)60);
        }
    }

    /// <summary>A GIF is never converted: SkiaSharp would keep only its first frame.</summary>
    [Fact]
    public async Task Should_Serve_A_Gif_As_Stored_To_A_Caller_That_Takes_Webp()
    {
        var file = await UploadAsync(SiteFileContainerNames.Images, "dot.gif", Gif);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Format = SiteFileFormats.Webp });

        content.ContentType.ShouldBe("image/gif");
        (await ReadAllAsync(content.GetStream())).ShouldBe(Gif);
    }

    /// <summary>
    /// Without WebP and without a size, a picture is served byte for byte, its EXIF and all. In-process, auto (and an
    /// unknown format) is the original: choosing by the request's Accept is the HTTP endpoint's.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(SiteFileFormats.Auto)]
    [InlineData(SiteFileFormats.Original)]
    [InlineData("avif")]
    public async Task Should_Serve_A_Jpeg_As_Stored_Unless_Webp_Is_Asked(string? format)
    {
        var jpeg = SidewaysJpeg();
        var file = await UploadAsync(SiteFileContainerNames.Images, "as-is.jpg", jpeg);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Format = format });

        content.ContentType.ShouldBe("image/jpeg");
        (await ReadAllAsync(content.GetStream())).ShouldBe(jpeg);
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
        var file = await UploadAsync(SiteFileContainerNames.Images, "dot.gif", Gif);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 10, Height = 10 });

        content.ContentType.ShouldBe("image/gif");
        (await ReadAllAsync(content.GetStream())).ShouldBe(Gif);
    }

    /// <summary>A 1 x 1 GIF89a.</summary>
    private static readonly byte[] Gif =
    {
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B
    };

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

    /// <summary>
    /// A PNG like a logo: a transparent background, a filled shape, text and thin lines - hard edges, no
    /// anti-aliasing, so every pixel is either fully transparent or fully opaque.
    /// </summary>
    private static byte[] TransparentDrawing(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var fill = new SKPaint { Color = new SKColor(30, 90, 200) };
            canvas.DrawRoundRect(width / 4f, 0, width * 3 / 4f, height, 20, 20, fill);
            using var text = new SKPaint { Color = SKColors.White };
            using var font = new SKFont { Size = height / 5f, Edging = SKFontEdging.Alias };
            canvas.DrawText("Logo Text", width / 3f, height / 2f, font, text);
            using var line = new SKPaint { Color = SKColors.Black, StrokeWidth = 1 };
            for (var y = 0; y < height; y += 7)
            {
                canvas.DrawLine(width / 4f, y, width, y, line);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A JPEG like a photo: smooth colour with fine texture, which WebP stores in fewer bytes.</summary>
    private static byte[] Photo(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            DrawPhoto(canvas, width, height);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private static void DrawPhoto(SKCanvas canvas, int width, int height)
    {
        using var gradient = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height),
                new[] { SKColors.SkyBlue, SKColors.OrangeRed, SKColors.ForestGreen }, SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, width, height, gradient);
        using var texture = new SKPaint
        {
            Shader = SKShader.CreatePerlinNoiseTurbulence(0.02f, 0.02f, 4, 7),
            BlendMode = SKBlendMode.Overlay
        };
        canvas.DrawRect(0, 0, width, height, texture);
    }

    /// <summary>
    /// A 400 x 200 JPEG, red on the left half and blue on the right, with an EXIF orientation of 6 (turn a quarter
    /// clockwise to display), as a phone writes a portrait photo.
    /// </summary>
    private static byte[] SidewaysJpeg()
    {
        using var bitmap = new SKBitmap(400, 200);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, 200, 200, red);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return TestFiles.WithExifOrientation(data.ToArray(), 6);
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
