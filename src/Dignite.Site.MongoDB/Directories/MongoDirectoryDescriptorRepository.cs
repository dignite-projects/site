using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.MongoDB;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Dignite.Site.Directories;

public class MongoDirectoryDescriptorRepository : MongoDbRepository<ISiteMongoDbContext, DirectoryDescriptor, Guid>, IDirectoryDescriptorRepository
{
    public MongoDirectoryDescriptorRepository(IMongoDbContextProvider<ISiteMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> NameExistsAsync(Guid creatorId, string containerName, string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .AnyAsync(
                d => d.ContainerName == containerName && d.CreatorId == creatorId && d.ParentId == parentId && d.Name == name,
                token);
    }

    public virtual async Task<List<DirectoryDescriptor>> GetListAsync(Guid creatorId, string containerName, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .Where(d => d.ContainerName == containerName && d.CreatorId == creatorId && d.ParentId == parentId)
            .ToListAsync(token);
    }

    public virtual async Task<int> GetMaxOrderAsync(Guid creatorId, string containerName, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        var orders = await (await GetQueryableAsync(token))
            .Where(d => d.ContainerName == containerName && d.CreatorId == creatorId && d.ParentId == parentId)
            .Select(d => d.Order)
            .ToListAsync(token);

        return orders.Count == 0 ? 0 : orders.Max();
    }

    public virtual async Task<List<DirectoryDescriptor>> GetAllByUserAsync(Guid creatorId, string containerName, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .Where(d => d.ContainerName == containerName && d.CreatorId == creatorId)
            .OrderBy(d => d.ParentId)
            .ThenBy(d => d.Order)
            .ToListAsync(token);
    }
}
