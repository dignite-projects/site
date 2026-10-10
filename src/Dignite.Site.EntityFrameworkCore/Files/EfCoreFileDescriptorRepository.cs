using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Files;

public class EfCoreFileDescriptorRepository : EfCoreRepository<ISiteDbContext, FileDescriptor, Guid>, IFileDescriptorRepository
{
    public EfCoreFileDescriptorRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> BlobNameExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(f => f.ContainerName == containerName && f.BlobName == blobName, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> ReferencingAnyAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(f => f.ContainerName == containerName && f.ReferBlobName == blobName, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<FileDescriptor?> FindByBlobNameAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(f => f.ContainerName == containerName && f.BlobName == blobName, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<FileDescriptor?> FindByHashAsync(string containerName, string hash, CancellationToken cancellationToken = default)
    {
        if (hash.IsNullOrEmpty())
        {
            return null;
        }

        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(f => f.ContainerName == containerName && f.Hash == hash, GetCancellationToken(cancellationToken));
    }

    public virtual async Task ClearDirectoryFromDeletedFilesAsync(Guid directoryId, CancellationToken cancellationToken = default)
    {
        await (await GetDbSetAsync())
            .IgnoreQueryFilters()
            .Where(f => f.IsDeleted && f.DirectoryId == directoryId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(f => f.DirectoryId, (Guid?)null),
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<FileDescriptor>> GetListAsync(
        string containerName,
        Guid? creatorId = null,
        Guid? directoryId = null,
        string? filter = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        return await (await GetListQueryAsync(containerName, creatorId, directoryId, filter))
            .OrderBy(FileDescriptorSorting.Normalize(sorting))
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCountAsync(
        string containerName,
        Guid? creatorId = null,
        Guid? directoryId = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetListQueryAsync(containerName, creatorId, directoryId, filter))
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual async Task<IQueryable<FileDescriptor>> GetListQueryAsync(
        string containerName,
        Guid? creatorId,
        Guid? directoryId,
        string? filter)
    {
        return (await GetDbSetAsync()).AsNoTracking()
            .Where(f => f.ContainerName == containerName)
            .WhereIf(directoryId.HasValue, f => f.DirectoryId == directoryId)
            .WhereIf(creatorId.HasValue, f => f.CreatorId == creatorId)
            .WhereIf(!filter.IsNullOrWhiteSpace(), f => f.Name.Contains(filter!));
    }
}
