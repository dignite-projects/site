using System;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Contents;

/// <summary>
/// One derived, typed projection of one field value of one content - the table that makes "find contents
/// where field X matches Y" a SQL query instead of an in-memory scan (总体设计 §2.4).
/// <para>
/// Derived, never authoritative: the real value always lives in the content's own <c>FlexFields</c> bag,
/// and every row here is re-derivable from it. Site needs exactly one such table, because <c>Content</c>
/// is its only flex-field host type.
/// </para>
/// <para>
/// Note that these rows key on <b>FieldId</b> while the value bag keys on <b>Field.Name</b>. That
/// asymmetry is deliberate (§2.4): renaming a field is a migration of every bag, but leaves every row
/// here untouched.
/// </para>
/// <para>
/// This shape is relational-only. It lives in the EF Core project because a pivot row is what a
/// relational store needs; the MongoDB provider has no equivalent and indexes the bag in place. Nothing
/// in Site' domain layer knows this type exists.
/// </para>
/// </summary>
public class ContentFlexFieldIndex : FlexFieldIndexBase<Content>, IMultiTenant
{
    protected ContentFlexFieldIndex()
    {
    }

    public ContentFlexFieldIndex(Guid id, Guid contentId, Guid fieldId, FlexFieldIndexValue value, Guid? tenantId)
        : base(id)
    {
        ContentId = contentId;
        TenantId = tenantId;
        SetValue(fieldId, value);
    }

    /// <summary>
    /// Site' own foreign key - the kernel maps no relationships, so the host declares and maps this
    /// itself.
    /// </summary>
    public virtual Guid ContentId { get; set; }

    /// <summary>
    /// Carried so ABP's data filter applies to this table directly.
    /// <para>
    /// Not strictly required for correctness - the query executor composes its subquery against an
    /// already tenant-filtered content query, so a foreign row could never survive the outer filter. It
    /// is here so that a query written against this table <i>without</i> going through that path is
    /// still confined to one tenant, rather than relying on every future caller remembering to join.
    /// </para>
    /// </summary>
    public virtual Guid? TenantId { get; set; }
}
