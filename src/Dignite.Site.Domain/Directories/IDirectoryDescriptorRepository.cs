using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Site.Directories;

/// <summary>
/// Every query is scoped to one owner (<c>creatorId</c>) and one container: directories are per user.
/// </summary>
public interface IDirectoryDescriptorRepository : IBasicRepository<DirectoryDescriptor, Guid>
{
    Task<bool> NameExistsAsync(Guid creatorId, string containerName, string name, Guid? parentId, CancellationToken cancellationToken = default);

    /// <summary>The direct children of <paramref name="parentId"/> (<c>null</c>: the top level).</summary>
    Task<List<DirectoryDescriptor>> GetListAsync(Guid creatorId, string containerName, Guid? parentId, CancellationToken cancellationToken = default);

    Task<int> GetMaxOrderAsync(Guid creatorId, string containerName, Guid? parentId, CancellationToken cancellationToken = default);

    /// <summary>The owner's whole tree in the container, ordered by parent and then position.</summary>
    Task<List<DirectoryDescriptor>> GetAllByUserAsync(Guid creatorId, string containerName, CancellationToken cancellationToken = default);
}
