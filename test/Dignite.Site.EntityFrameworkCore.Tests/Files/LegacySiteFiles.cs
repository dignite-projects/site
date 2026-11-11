using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Files;

/// <summary>
/// Writes a file the way Site's own file library stored it before Site used the Drive: the bytes under a new blob name
/// in the container, and a <see cref="FileDescriptor"/> row that owns them. The library is gone, so tests that read
/// such files - the public read endpoint's fallback, the migration to the Drive - make them with this. Like the library
/// since 10.7.5, a JPEG for <see cref="SiteFileContainerNames.Images"/> is stored as lossy WebP, turned upright, and named
/// <c>.webp</c>; everything else is stored as given, and nothing is resized.
/// </summary>
public class LegacySiteFiles : ITransientDependency
{
    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IBlobContainerFactory BlobContainerFactory { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public LegacySiteFiles(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        ICurrentTenant currentTenant)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        BlobContainerFactory = blobContainerFactory;
        CurrentTenant = currentTenant;
    }

    /// <summary>
    /// Stores <paramref name="bytes"/> as a file named <paramref name="fileName"/>; its type is the one
    /// <see cref="SiteFileTypes"/> gives the name's extension.
    /// </summary>
    public virtual async Task<FileDescriptor> CreateAsync(string containerName, string fileName, byte[] bytes, Guid? directoryId = null)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (containerName == SiteFileContainerNames.Images &&
            Path.GetExtension(fileName).ToLowerInvariant() is ".jpg" or ".jpeg")
        {
            bytes = ToUprightWebp(bytes);
            fileName = Path.ChangeExtension(fileName, ".webp");
        }

        var extension = Path.GetExtension(fileName);
        var type = SiteFileTypes.All.FirstOrDefault(t => t.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
        var blobName = Guid.NewGuid().ToString("N") + (type?.Extension ?? string.Empty);

        await BlobContainerFactory.Create(containerName).SaveAsync(blobName, bytes);

        return await FileDescriptorRepository.InsertAsync(
            new FileDescriptor(
                Guid.NewGuid(), containerName, blobName, fileName, type?.MimeType ?? "application/octet-stream", bytes.Length,
                hash: hash, referBlobName: null, directoryId, CurrentTenant.Id),
            autoSave: true);
    }

    private static byte[] ToUprightWebp(byte[] jpeg)
    {
        using var codec = SKCodec.Create(new MemoryStream(jpeg));
        using var decoded = SKBitmap.Decode(codec);
        using var upright = ImageOrientation.Orient(decoded, codec.EncodedOrigin);
        using var image = SKImage.FromBitmap(upright);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 85);
        return data.ToArray();
    }

    /// <summary>
    /// A second file with the bytes of <paramref name="owner"/>, as the library's content dedup stored one: an address of
    /// its own, no bytes of its own, and no hash.
    /// </summary>
    public virtual async Task<FileDescriptor> CreateReferenceAsync(FileDescriptor owner, string fileName, Guid? directoryId = null)
    {
        var blobName = Guid.NewGuid().ToString("N") + Path.GetExtension(owner.BlobName);

        return await FileDescriptorRepository.InsertAsync(
            new FileDescriptor(
                Guid.NewGuid(), owner.ContainerName, blobName, fileName, owner.MimeType, owner.Size,
                hash: null, referBlobName: owner.GetActualBlobName(), directoryId, CurrentTenant.Id),
            autoSave: true);
    }
}
