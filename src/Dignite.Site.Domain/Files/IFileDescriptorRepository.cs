using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Site.Files;

public interface IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
{
    /// <summary>Whether a live descriptor has <paramref name="blobName"/> as its own blob name.</summary>
    Task<bool> BlobNameExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>Whether a live descriptor is a reference to <paramref name="blobName"/>.</summary>
    Task<bool> ReferencingAnyAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    Task<FileDescriptor?> FindByBlobNameAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>The live owner of the blob whose content hashes to <paramref name="hash"/>, if any.</summary>
    Task<FileDescriptor?> FindByHashAsync(string containerName, string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches soft-deleted descriptors from <paramref name="directoryId"/>, bypassing the soft-delete
    /// filter, so the directory can be deleted while their audit rows are kept.
    /// </summary>
    Task ClearDirectoryFromDeletedFilesAsync(Guid directoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="creatorId"/> and <paramref name="directoryId"/> filter only when given:
    /// <c>null</c> means every creator and every directory, not "no creator" or "the root".
    /// </summary>
    Task<List<FileDescriptor>> GetListAsync(
        string containerName,
        Guid? creatorId = null,
        Guid? directoryId = null,
        string? filter = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(
        string containerName,
        Guid? creatorId = null,
        Guid? directoryId = null,
        string? filter = null,
        CancellationToken cancellationToken = default);
}
