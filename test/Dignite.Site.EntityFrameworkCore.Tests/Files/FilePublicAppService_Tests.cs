using System.IO;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Public.Files;
using Shouldly;
using SixLabors.ImageSharp;
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
        var file = await UploadAsync(SiteFileContainerNames.Default, "served.pdf", bytes);

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

        var info = Image.Identify(await ReadAllAsync(content.GetStream()));
        info.Width.ShouldBe(120);
        info.Height.ShouldBe(63);
        content.ContentType.ShouldBe("image/jpeg");
    }

    /// <summary>A size asked of a file that is not an image is ignored, not an error.</summary>
    [Fact]
    public async Task Should_Serve_A_Non_Image_As_Is_When_A_Size_Is_Asked()
    {
        var bytes = TestFiles.Pdf("not resizable");
        var file = await UploadAsync(SiteFileContainerNames.Default, "doc.pdf", bytes);

        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput { Width = 100 });

        (await ReadAllAsync(content.GetStream())).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Not_Serve_A_Deleted_File()
    {
        var file = await UploadAsync(SiteFileContainerNames.Default, "deleted.pdf", TestFiles.Pdf("deleted"));
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
        var file = await UploadAsync(SiteFileContainerNames.Default, "mine.pdf", TestFiles.Pdf("mine"));

        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _filePublicAppService.GetAsync("invoices", file.BlobName, new GetFileInput()));
        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _filePublicAppService.GetAsync(SiteFileContainerNames.Images, file.BlobName, new GetFileInput()));
        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _filePublicAppService.GetAsync(SiteFileContainerNames.Default, "does-not-exist", new GetFileInput()));
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string fileName, byte[] bytes)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, fileName, stream);
        });
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
