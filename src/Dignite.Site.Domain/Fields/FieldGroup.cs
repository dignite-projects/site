using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Fields;

/// <summary>
/// An organizational grouping over the field library (总体设计 §2.3, ≈ Dignite.Cms's <c>FieldGroup</c>).
/// Purely for navigating a large library - nothing at runtime reads it, and a field without a group is
/// entirely normal.
/// <para>
/// Deliberately minimal: a <see cref="Name"/> and an <see cref="Order"/>, nothing else. A second display
/// label would have to be kept in step with the first for no gain, since the only thing that ever renders
/// a group is a field-library listing; Dignite.Cms's <c>FieldGroup</c> is the same shape for the same
/// reason.
/// </para>
/// <para>
/// <see cref="BasicAggregateRoot{TKey}"/> rather than <c>AggregateRoot</c>: it is an aggregate root
/// (own repository, own lifetime, referenced by <see cref="Field"/> only by id), but the two things
/// <c>AggregateRoot</c> adds both cost a column and neither earns it here. <c>ExtraProperties</c> exists
/// so a host can extend a module's entity without forking it, which is not something a naming label
/// invites; and <c>ConcurrencyStamp</c> guards lost updates on an entity whose entire mutable state is a
/// string and an int, where the loser of a concurrent edit simply edits it again. <c>Field</c> and
/// <c>Content</c> keep the full base classes precisely because the opposite is true of them.
/// </para>
/// </summary>
public class FieldGroup : BasicAggregateRoot<Guid>, IMultiTenant
{
    protected FieldGroup()
    {
        Fields = new List<Field>();
    }

    public FieldGroup(Guid id, string name, int order = 0, Guid? tenantId = null)
        : base(id)
    {
        SetName(name);
        Order = order;
        TenantId = tenantId;

        Fields = new List<Field>();
    }

    /// <summary>Unique within the tenant.</summary>
    public virtual string Name { get; protected set; } = default!;

    /// <summary>Display ordering within the field library, e.g. for a group picker (总体设计 §2.3).</summary>
    public virtual int Order { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// The fields filed under this group. Deleting a group does not delete them - the mapping sets their
    /// <c>GroupId</c> back to null, since a grouping is a filing decision and not the field's existence.
    /// </summary>
    public virtual ICollection<Field> Fields { get; protected set; }

    public virtual void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), FieldGroupConsts.MaxNameLength);
    }

    public virtual void SetOrder(int order)
    {
        Order = order;
    }
}
