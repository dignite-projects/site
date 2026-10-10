using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Directories;

/// <summary>
/// A folder in one file container of Site's file library. Directories belong to the user who created
/// them (<see cref="AuditedAggregateRoot{TKey}.CreatorId"/>): each editor files uploads in a tree of their
/// own, and every rule in <see cref="DirectoryManager"/> is scoped to that owner.
/// </summary>
public class DirectoryDescriptor : AuditedAggregateRoot<Guid>, IMultiTenant
{
    protected DirectoryDescriptor()
    {
    }

    public DirectoryDescriptor(Guid id, string containerName, string name, Guid? parentId, int order, Guid? tenantId)
        : base(id)
    {
        ContainerName = Check.NotNullOrWhiteSpace(containerName, nameof(containerName));
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
        ParentId = parentId;
        Order = order;
        TenantId = tenantId;
    }

    public virtual string ContainerName { get; protected set; } = default!;

    public virtual string Name { get; protected set; } = default!;

    public virtual Guid? ParentId { get; protected set; }

    /// <summary>Position among its siblings.</summary>
    public virtual int Order { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// Renames the directory. Uniqueness among siblings is enforced by <see cref="DirectoryManager"/>.
    /// </summary>
    public virtual void Rename(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
    }

    /// <summary>
    /// Re-parents the directory and sets its position. Cycle and container checks live in
    /// <see cref="DirectoryManager.MoveAsync"/>; never call this to bypass them.
    /// </summary>
    public virtual void MoveTo(Guid? parentId, int order)
    {
        ParentId = parentId;
        Order = order;
    }

    public virtual void SetOrder(int order)
    {
        Order = order;
    }
}
