using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Directories;

public class EfCoreDirectoryDescriptorRepository : EfCoreRepository<ISiteDbContext, DirectoryDescriptor, Guid>, IDirectoryDescriptorRepository
{
    public EfCoreDirectoryDescriptorRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> NameExistsAsync(Guid creatorId, string containerName, string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(
                d => d.ContainerName == containerName && d.CreatorId == creatorId && d.ParentId == parentId && d.Name == name,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DirectoryDescriptor>> GetListAsync(Guid creatorId, string containerName, Guid? parentId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(d => d.ContainerName == containerName && d.CreatorId == creatorId && d.ParentId == parentId)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<int> GetMaxOrderAsync(Guid creatorId, string containerName, Guid? parentId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(d => d.ContainerName == containerName && d.CreatorId == creatorId && d.ParentId == parentId)
            .MaxAsync(d => (int?)d.Order, GetCancellationToken(cancellationToken)) ?? 0;
    }

    public virtual async Task<List<DirectoryDescriptor>> GetAllByUserAsync(Guid creatorId, string containerName, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AsNoTracking()
            .Where(d => d.ContainerName == containerName && d.CreatorId == creatorId)
            .OrderBy(d => d.ParentId)
            .ThenBy(d => d.Order)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
