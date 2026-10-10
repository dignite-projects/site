using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Files;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Directories;

/// <summary>
/// The rules that keep each user's directories a tree inside one container: unique sibling names, a parent
/// in the same container that belongs to the same owner and tenant, no cycles, and deletion only when empty.
/// </summary>
public class DirectoryManager : DomainService
{
    protected IDirectoryDescriptorRepository DirectoryDescriptorRepository { get; }

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    public DirectoryManager(
        IDirectoryDescriptorRepository directoryDescriptorRepository,
        IFileDescriptorRepository fileDescriptorRepository)
    {
        DirectoryDescriptorRepository = directoryDescriptorRepository;
        FileDescriptorRepository = fileDescriptorRepository;
    }

    public virtual async Task<DirectoryDescriptor> CreateAsync(
        Guid userId,
        string containerName,
        string name,
        Guid? parentId = null,
        CancellationToken cancellationToken = default)
    {
        FileContainerNotAvailableException.ThrowIfNotSiteContainer(containerName);

        if (parentId.HasValue)
        {
            var parent = await DirectoryDescriptorRepository.FindAsync(parentId.Value, false, cancellationToken);
            if (parent == null ||
                !parent.ContainerName.Equals(containerName, StringComparison.Ordinal) ||
                parent.TenantId != CurrentTenant.Id ||
                parent.CreatorId != userId)
            {
                throw new DirectoryNotFoundBusinessException();
            }
        }

        if (await DirectoryDescriptorRepository.NameExistsAsync(userId, containerName, name, parentId, cancellationToken))
        {
            throw new DirectoryNameAlreadyExistException(name);
        }

        var order = await DirectoryDescriptorRepository.GetMaxOrderAsync(userId, containerName, parentId, cancellationToken);
        var directory = new DirectoryDescriptor(
            GuidGenerator.Create(),
            containerName,
            name,
            parentId,
            order + 1,
            CurrentTenant.Id)
        {
            CreatorId = userId
        };

        return await DirectoryDescriptorRepository.InsertAsync(directory, cancellationToken: cancellationToken);
    }

    public virtual async Task<DirectoryDescriptor> UpdateAsync(
        DirectoryDescriptor directory,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (!directory.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) &&
            await DirectoryDescriptorRepository.NameExistsAsync(
                GetOwnerId(directory),
                directory.ContainerName,
                name,
                directory.ParentId,
                cancellationToken))
        {
            throw new DirectoryNameAlreadyExistException(name);
        }

        directory.Rename(name);
        return await DirectoryDescriptorRepository.UpdateAsync(directory, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Re-parents <paramref name="directory"/> under <paramref name="parentId"/> (<c>null</c>: the top level)
    /// at <paramref name="order"/>, shifting the siblings at or after that position down by one.
    /// </summary>
    public virtual async Task<DirectoryDescriptor> MoveAsync(
        DirectoryDescriptor directory,
        Guid? parentId,
        int order,
        CancellationToken cancellationToken = default)
    {
        var ownerId = GetOwnerId(directory);

        if (parentId.HasValue)
        {
            var parent = await DirectoryDescriptorRepository.GetAsync(parentId.Value, cancellationToken: cancellationToken);
            if (!parent.ContainerName.Equals(directory.ContainerName, StringComparison.Ordinal))
            {
                throw new DirectoryCannotMoveAcrossContainersException();
            }

            if (parent.CreatorId != ownerId || parent.TenantId != directory.TenantId)
            {
                throw new DirectoryNotFoundBusinessException();
            }

            if (await IsDescendantOrSelfAsync(directory.Id, parent, cancellationToken))
            {
                throw new DirectoryCannotMoveIntoItselfException();
            }
        }

        var siblings = await DirectoryDescriptorRepository.GetListAsync(
            ownerId,
            directory.ContainerName,
            parentId,
            cancellationToken);
        foreach (var sibling in siblings.Where(d => d.Order >= order && d.Id != directory.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            sibling.SetOrder(sibling.Order + 1);
            await DirectoryDescriptorRepository.UpdateAsync(sibling, cancellationToken: cancellationToken);
        }

        directory.MoveTo(parentId, order);
        return await DirectoryDescriptorRepository.UpdateAsync(directory, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Refuses a directory that still has subdirectories or live files. Soft-deleted files do not count:
    /// <see cref="DeleteAsync"/> detaches them so their audit rows survive the directory.
    /// </summary>
    public virtual async Task EnsureEmptyAsync(DirectoryDescriptor directory, CancellationToken cancellationToken = default)
    {
        var children = await DirectoryDescriptorRepository.GetListAsync(
            GetOwnerId(directory),
            directory.ContainerName,
            directory.Id,
            cancellationToken);
        if (children.Count != 0)
        {
            throw new DirectoryHasChildrenException();
        }

        var files = await FileDescriptorRepository.GetListAsync(
            directory.ContainerName,
            directoryId: directory.Id,
            maxResultCount: 1,
            cancellationToken: cancellationToken);
        if (files.Count != 0)
        {
            throw new DirectoryHasFilesException();
        }
    }

    /// <summary>
    /// Deletes an empty directory. The application service runs this in one transactional unit of work, so
    /// the emptiness check and the delete cannot be separated by another request's write.
    /// </summary>
    public virtual async Task DeleteAsync(DirectoryDescriptor directory, CancellationToken cancellationToken = default)
    {
        await EnsureEmptyAsync(directory, cancellationToken);
        await FileDescriptorRepository.ClearDirectoryFromDeletedFilesAsync(directory.Id, cancellationToken);
        await DirectoryDescriptorRepository.DeleteAsync(directory, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Walks up from <paramref name="parent"/>; a revisited node means the stored tree already has a cycle,
    /// which is treated the same as reaching <paramref name="directoryId"/>.
    /// </summary>
    protected virtual async Task<bool> IsDescendantOrSelfAsync(
        Guid directoryId,
        DirectoryDescriptor parent,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var current = parent;

        while (true)
        {
            if (current.Id == directoryId || !visited.Add(current.Id))
            {
                return true;
            }

            if (!current.ParentId.HasValue)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            current = await DirectoryDescriptorRepository.GetAsync(current.ParentId.Value, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// A directory always has an owner - <see cref="CreateAsync"/> sets it. One without (a row written some
    /// other way) cannot be placed in anyone's tree, so it is treated as missing.
    /// </summary>
    protected virtual Guid GetOwnerId(DirectoryDescriptor directory)
    {
        return directory.CreatorId ?? throw new DirectoryNotFoundBusinessException();
    }
}
