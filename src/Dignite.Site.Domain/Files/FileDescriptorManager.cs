using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Files;

/// <summary>
/// Adds files to Site's file library and removes them. The bytes go through <see cref="IFileStorer"/> -
/// the one runner of a container's upload pipeline (capped copy, MIME detection from the content, the
/// container's handlers, SHA-256, a generated blob name, a no-overwrite save). This class owns only what
/// the library adds on top: the descriptor row, content dedup by <see cref="StoredFileInfo.Hash"/>, and
/// keeping blob and row from drifting apart.
/// </summary>
public class FileDescriptorManager : DomainService
{
    /// <summary>
    /// Compensation runs on its own bounded token: when the request was cancelled, reusing the request
    /// token would cancel the cleanup at once and leave the blob behind.
    /// </summary>
    protected static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(30);

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IFileStorer FileStorer { get; }

    protected IBlobContainerFactory BlobContainerFactory { get; }

    public FileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IFileStorer fileStorer,
        IBlobContainerFactory blobContainerFactory)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        FileStorer = fileStorer;
        BlobContainerFactory = blobContainerFactory;
    }

    /// <summary>
    /// Stores <paramref name="stream"/> in <paramref name="containerName"/> and records it.
    /// <para>
    /// Order (file-storing invariant §4): the blob is stored first, then the row is written with
    /// <c>autoSave</c>, and a failed write deletes the blob again. When the stored bytes already exist in
    /// the container, the new row becomes a reference to the existing blob and the copy just stored is
    /// deleted - but only after the row is saved, so a failure never leaves the row pointing at nothing.
    /// </para>
    /// <para>
    /// The dedup lookup is check-then-act: two simultaneous uploads of the same bytes can both miss it.
    /// The live-rows-only unique index on (tenant, container, hash) is the arbiter - the loser's insert
    /// fails, its blob is deleted, and a retry finds the winner and becomes a reference.
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

        var stored = await FileStorer.StoreAsync(containerName, name, stream, cancellationToken);

        FileDescriptor? owner = null;
        try
        {
            owner = await FileDescriptorRepository.FindByHashAsync(containerName, stored.Hash, cancellationToken);

            var file = owner == null
                ? new FileDescriptor(
                    GuidGenerator.Create(), containerName, stored.BlobName, name, stored.MimeType, stored.Size,
                    hash: stored.Hash, referBlobName: null, directoryId, CurrentTenant.Id)
                : new FileDescriptor(
                    GuidGenerator.Create(), containerName, stored.BlobName, name, stored.MimeType, stored.Size,
                    hash: null, referBlobName: owner.GetActualBlobName(), directoryId, CurrentTenant.Id);

            await FileDescriptorRepository.InsertAsync(file, autoSave: true, cancellationToken);

            if (owner != null)
            {
                // The bytes already had a home; the copy only existed to learn its hash.
                await TryDeleteBlobAsync(containerName, stored.BlobName);
            }

            return file;
        }
        catch
        {
            await TryDeleteBlobAsync(containerName, stored.BlobName);
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

        await FileStorer.DeleteAsync(file.ContainerName, blobName, cancellationToken);
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

    protected virtual async Task TryDeleteBlobAsync(string containerName, string blobName)
    {
        using var compensation = new CancellationTokenSource(CompensationTimeout);
        try
        {
            await FileStorer.DeleteAsync(containerName, blobName, compensation.Token);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }
}
