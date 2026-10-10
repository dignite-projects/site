using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.MongoDB;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Dignite.Site.Files;

public class MongoFileDescriptorRepository : MongoDbRepository<ISiteMongoDbContext, FileDescriptor, Guid>, IFileDescriptorRepository
{
    public MongoFileDescriptorRepository(IMongoDbContextProvider<ISiteMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> BlobNameExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .AnyAsync(f => f.ContainerName == containerName && f.BlobName == blobName, token);
    }

    public virtual async Task<bool> ReferencingAnyAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .AnyAsync(f => f.ContainerName == containerName && f.ReferBlobName == blobName, token);
    }

    public virtual async Task<FileDescriptor?> FindByBlobNameAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return await FindAsync(
            f => f.ContainerName == containerName && f.BlobName == blobName,
            includeDetails: false,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<FileDescriptor?> FindByHashAsync(string containerName, string hash, CancellationToken cancellationToken = default)
    {
        if (hash.IsNullOrEmpty())
        {
            return null;
        }

        return await FindAsync(
            f => f.ContainerName == containerName && f.Hash == hash,
            includeDetails: false,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task ClearDirectoryFromDeletedFilesAsync(Guid directoryId, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        var collection = await GetCollectionAsync(token);
        var filter = Builders<FileDescriptor>.Filter.And(
            Builders<FileDescriptor>.Filter.Eq(f => f.IsDeleted, true),
            Builders<FileDescriptor>.Filter.Eq(f => f.DirectoryId, directoryId));
        var update = Builders<FileDescriptor>.Update.Set(f => f.DirectoryId, null);

        await collection.UpdateManyAsync(filter, update, cancellationToken: token);
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
        var token = GetCancellationToken(cancellationToken);
        return await (await GetListQueryAsync(containerName, creatorId, directoryId, filter, token))
            .OrderBy(FileDescriptorSorting.Normalize(sorting))
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(token);
    }

    public virtual async Task<long> GetCountAsync(
        string containerName,
        Guid? creatorId = null,
        Guid? directoryId = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetListQueryAsync(containerName, creatorId, directoryId, filter, token))
            .LongCountAsync(token);
    }

    protected virtual async Task<IQueryable<FileDescriptor>> GetListQueryAsync(
        string containerName,
        Guid? creatorId,
        Guid? directoryId,
        string? filter,
        CancellationToken cancellationToken)
    {
        return (await GetQueryableAsync(cancellationToken))
            .Where(f => f.ContainerName == containerName)
            .WhereIf(directoryId.HasValue, f => f.DirectoryId == directoryId)
            .WhereIf(creatorId.HasValue, f => f.CreatorId == creatorId)
            .WhereIf(!filter.IsNullOrWhiteSpace(), f => f.Name.Contains(filter!));
    }
}
