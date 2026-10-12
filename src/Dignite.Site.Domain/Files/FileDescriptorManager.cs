using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Files;

/// <summary>
/// Adds files to Site's file library and removes them. The upload rules themselves - size cap, allowed
/// content types, image resize and compression - are the container's BLOB pipeline contributors
/// (<c>Dignite.Abp.BlobStoring.Pipeline</c> / <c>.Imaging</c>, configured by <c>SiteAdminApplicationModule</c>),
/// which run inside <c>IBlobContainer.SaveAsync</c>. This class
/// is the caller around them: it buffers and detects the upload, hashes it, names the blob, writes the
/// descriptor row, dedups by content, and keeps blob and row from drifting apart.
/// </summary>
public class FileDescriptorManager : DomainService
{
    /// <summary>
    /// Compensation runs on its own bounded token: when the request was cancelled, reusing the request
    /// token would cancel the cleanup at once and leave the blob behind.
    /// </summary>
    protected static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(30);

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IBlobContainerFactory BlobContainerFactory { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    protected IMimeTypeDetector MimeTypeDetector { get; }

    public FileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        BlobContainerFactory = blobContainerFactory;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
        MimeTypeDetector = mimeTypeDetector;
    }

    /// <summary>
    /// Stores <paramref name="stream"/> in <paramref name="containerName"/> and records it.
    /// <para>
    /// Sequence: the upload is copied once into memory, capped at what the container's pipeline enforces
    /// (its <c>MaxSize</c> contributor, else <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>), so
    /// an oversized upload fails with <c>Dignite.Abp.BlobStoring:ContentTooLarge</c> before anything is stored.
    /// Its type is detected from the bytes and reconciled with the extension of <paramref name="fileName"/>
    /// (<c>Dignite.Abp.BlobStoring:ContentTypeMismatch</c>), and it is hashed (SHA-256, upper-case hex).
    /// </para>
    /// <para>
    /// Dedup comes before any write: when a live descriptor in the same container (and tenant) already owns
    /// these bytes, the new descriptor becomes a <i>reference</i> to that blob and no blob is written at all.
    /// The reference gets an address of its own, the owner's <see cref="FileDescriptor.Size"/> and no hash. A
    /// dedup hit skips the container's contributors - the bytes passed them when the owner was stored.
    /// </para>
    /// <para>
    /// Otherwise the blob is saved under a new name (32 random hex digits plus the canonical extension of the
    /// detected type, see <see cref="CreateBlobName"/>) without overwriting (the contributors run here and may
    /// reject the upload), then the row is written with <c>autoSave</c>, and a failed write deletes the blob
    /// again. The lookup is check-then-act: two simultaneous uploads of the same bytes can both miss it. The
    /// live-rows-only unique index on (tenant, container, hash) is the arbiter - the loser's insert fails, its
    /// blob is deleted, and a retry finds the winner and becomes a reference.
    /// </para>
    /// <para>
    /// What the row records: <see cref="FileDescriptor.MimeType"/> is the detected type (resizing and
    /// compressing keep the format); <see cref="FileDescriptor.Hash"/> is the SHA-256 of the <b>upload</b>, so the
    /// same upload dedups whether or not the container transformed it; <see cref="FileDescriptor.Size"/> is the
    /// size of the <b>stored</b> bytes - what the public endpoint serves - read back from the container for an
    /// image (the only content an image contributor can change) and the upload's length otherwise.
    /// </para>
    /// </summary>
    /// <param name="fileName">The uploader's file name: its extension is checked against the content, and
    /// it becomes the display name (path stripped, shortened to fit).</param>
    public virtual async Task<FileDescriptor> CreateAsync(
        string containerName,
        string fileName,
        Stream stream,
        Guid? directoryId = null,
        CancellationToken cancellationToken = default)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(containerName);
        Check.NotNull(stream, nameof(stream));
        var name = NormalizeName(fileName);

        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(BlobContainerConfigurationProvider.Get(containerName));
        await using var content = await BlobStreamBuffering.CopyToBufferAsync(stream, maxBytes, cancellationToken);

        var mimeType = await MimeTypeDetector.DetectAsync(content, name, cancellationToken);
        var hash = await ComputeHashAsync(content, cancellationToken);
        var blobName = CreateBlobName(mimeType);

        var owner = await FileDescriptorRepository.FindByHashAsync(containerName, hash, cancellationToken);
        if (owner != null)
        {
            // The bytes already have a home: nothing is stored, the new row only points at it.
            return await FileDescriptorRepository.InsertAsync(
                new FileDescriptor(
                    GuidGenerator.Create(), containerName, blobName, name, mimeType, owner.Size,
                    hash: null, referBlobName: owner.GetActualBlobName(), directoryId, CurrentTenant.Id),
                autoSave: true,
                cancellationToken);
        }

        // Taken before the save: the content belongs to the pipeline while it runs.
        var uploadLength = content.Length;
        var container = BlobContainerFactory.Create(containerName);
        try
        {
            await container.SaveAsync(blobName, content, overrideExisting: false, cancellationToken);
        }
        catch (Exception exception) when (exception is not BlobAlreadyExistsException and not BusinessException)
        {
            // The provider failed after it may have written bytes. A rejection by a contributor (a
            // BusinessException) happens before the provider writes anything, and a name collision means the
            // blob belongs to someone else - neither is compensated.
            await TryDeleteBlobAsync(container, blobName);
            throw;
        }

        try
        {
            var size = await GetStoredSizeAsync(container, blobName, mimeType, uploadLength, cancellationToken);

            return await FileDescriptorRepository.InsertAsync(
                new FileDescriptor(
                    GuidGenerator.Create(), containerName, blobName, name, mimeType, size,
                    hash: hash, referBlobName: null, directoryId, CurrentTenant.Id),
                autoSave: true,
                cancellationToken);
        }
        catch
        {
            await TryDeleteBlobAsync(container, blobName);
            throw;
        }
    }

    /// <summary>
    /// Deletes the descriptor (soft) and then its blob, unless another live descriptor still uses it: a
    /// reference to it, or - for a reference being deleted - the owner itself.
    /// <para>
    /// The row is deleted with <c>autoSave</c> before the reference checks run, so this descriptor does not
    /// count itself. A crash between the two steps leaves an unreferenced blob - unreachable bytes, never
    /// lost data: ABP's blob containers cannot be enumerated, so reclaiming such orphans belongs to tooling
    /// on the concrete storage backend, not to an outbox here.
    /// </para>
    /// </summary>
    public virtual async Task DeleteAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        await FileDescriptorRepository.DeleteAsync(file, autoSave: true, cancellationToken);

        var blobName = file.GetActualBlobName();
        if (await FileDescriptorRepository.ReferencingAnyAsync(file.ContainerName, blobName, cancellationToken))
        {
            return;
        }

        if (file.IsReference() &&
            await FileDescriptorRepository.BlobNameExistsAsync(file.ContainerName, blobName, cancellationToken))
        {
            return;
        }

        await BlobContainerFactory.Create(file.ContainerName).DeleteAsync(blobName, cancellationToken);
    }

    /// <summary>
    /// The live descriptor addressed by <paramref name="blobName"/> in one of Site's containers, or
    /// <c>null</c> - for a container that is not Site's as much as for a name that does not exist.
    /// </summary>
    public virtual async Task<FileDescriptor?> FindAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (!SiteFileContainerNames.Contains(containerName) || blobName.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await FileDescriptorRepository.FindByBlobNameAsync(containerName, blobName, cancellationToken);
    }

    /// <summary>
    /// The bytes of <paramref name="file"/> (its own blob, or the one it refers to), or <c>null</c> when the
    /// blob is missing from storage.
    /// </summary>
    public virtual Task<Stream?> GetStreamOrNullAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        return BlobContainerFactory.Create(file.ContainerName).GetOrNullAsync(file.GetActualBlobName(), cancellationToken);
    }

    /// <summary>
    /// The display name for an upload: the file name without any path a client sent along, shortened to
    /// <see cref="FileDescriptorConsts.MaxNameLength"/> by cutting the stem, so the extension - which the
    /// content was checked against - survives.
    /// </summary>
    protected virtual string NormalizeName(string fileName)
    {
        Check.NotNullOrWhiteSpace(fileName, nameof(fileName));

        var name = Path.GetFileName(fileName.Replace('\\', '/').TrimEnd('/')).Trim();
        if (name.IsNullOrWhiteSpace())
        {
            name = fileName.Trim();
        }

        var maxLength = FileDescriptorConsts.MaxNameLength;
        if (name.Length <= maxLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        if (extension.Length >= maxLength)
        {
            return name[..maxLength];
        }

        return name[..(maxLength - extension.Length)] + extension;
    }


    /// <summary>
    /// A new blob name: 32 hexadecimal digits of a random GUID plus the canonical extension of
    /// <paramref name="mimeType"/> from <see cref="SiteFileTypes"/> (none when the table has no row for it).
    /// <see cref="Guid.NewGuid"/> rather than the sequential <c>IGuidGenerator</c>: the name is the public
    /// address of the file, so it stays fully random.
    /// <para>
    /// The extension is derived from the <b>detected</b> type, never copied from the uploader's file name (a PNG
    /// named <c>.jpg</c> is stored as <c>.png</c>). That way the contributors, which only see the blob name
    /// inside <c>SaveAsync</c> and reconcile the content with its extension, agree with the content: a compound
    /// file named <c>.doc</c> that the signature engine cannot name is still accepted as
    /// <c>application/msword</c>, and an empty file takes its extension's type.
    /// </para>
    /// <para>
    /// Blobs stored before this change have no extension and are served as before: the read routes are
    /// catch-all.
    /// </para>
    /// </summary>
    protected virtual string CreateBlobName(string mimeType)
    {
        return Guid.NewGuid().ToString("N") + (SiteFileTypes.GetExtension(mimeType) ?? string.Empty);
    }

    /// <summary>
    /// SHA-256 of the content as upper-case hex (64 characters, <see cref="FileDescriptorConsts.MaxHashLength"/>);
    /// the stream is rewound afterwards.
    /// </summary>
    protected virtual async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// The size of what was stored under <paramref name="blobName"/>. The image contributors only ever touch
    /// <c>image/*</c> content (everything else passes through them untouched), so for anything else the stored
    /// size is the upload's <paramref name="uploadLength"/>; an image may have been resized or re-compressed, so
    /// its size is read back from the container.
    /// </summary>
    protected virtual async Task<long> GetStoredSizeAsync(
        IBlobContainer container,
        string blobName,
        string mimeType,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return uploadLength;
        }

        await using var stored = await container.GetAsync(blobName, cancellationToken);
        if (stored.CanSeek)
        {
            return stored.Length;
        }

        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await stored.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            length += read;
        }

        return length;
    }

    protected virtual async Task TryDeleteBlobAsync(IBlobContainer container, string blobName)
    {
        using var compensation = new CancellationTokenSource(CompensationTimeout);
        try
        {
            await container.DeleteAsync(blobName, compensation.Token);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }
}
