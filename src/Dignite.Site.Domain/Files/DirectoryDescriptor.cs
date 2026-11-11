using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Files;

/// <summary>
/// A folder in one file container of the file library Site kept before its files moved to the Drive. Directories belong
/// to the user who created them (<see cref="AuditedAggregateRoot{TKey}.CreatorId"/>): each editor filed uploads in a
/// tree of their own. <b>Legacy and read-only</b>, like <see cref="FileDescriptor"/>: the migration to the Drive reads
/// it, and the next major version removes it with its table.
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
    /// Renames the directory. Uniqueness among siblings was enforced by the library's directory manager.
    /// </summary>
    public virtual void Rename(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
    }

    /// <summary>
    /// Re-parents the directory and sets its position. Cycle and container checks live in
    /// the library's directory manager; never call this to bypass them.
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
