using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Pages;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.ContentTypes;

/// <summary>
/// A shape of content: a named arrangement of fields, belonging to one <see cref="Page"/>
/// (总体设计 §2.3).
/// <para>
/// A content type owns no route, no template and no cardinality flag - those are the page's business or
/// a runtime decision. What it owns is the arrangement: which fields from the tenant's field library
/// this kind of content has, and how each of them is used here.
/// </para>
/// <para>
/// It belongs to exactly one page and is never shared across pages. Reuse lives one level down: the same
/// <c>Field</c> can be pulled into any number of content types, on any number of pages (§2.5). So
/// <c>/blog</c> having three types (article, gallery, video) that all use the same <c>title</c> field is
/// the normal case, and widening <c>title</c>'s character limit is one edit to one row.
/// </para>
/// </summary>
public class ContentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    private List<ContentTypeField> _fields = new();

    protected ContentType()
    {
    }

    public ContentType(
        Guid id,
        Guid pageId,
        string name,
        string displayName,
        string? description = null,
        IEnumerable<ContentTypeField>? fields = null,
        Guid? tenantId = null)
        : base(id)
    {
        PageId = pageId;
        SetName(name);
        SetDisplayName(displayName);
        Description = description;
        TenantId = tenantId;

        SetFields(fields ?? Enumerable.Empty<ContentTypeField>());
    }

    /// <summary>The page this type is built beneath. Dignite.Cms's <c>EntryType.SectionId</c>.</summary>
    public virtual Guid PageId { get; protected set; }

    /// <summary>Unique within the owning page, e.g. <c>post-article</c>.</summary>
    public virtual string Name { get; protected set; } = default!;

    public virtual string DisplayName { get; protected set; } = default!;

    /// <summary>
    /// Doubles as the description an AI client reads over MCP to understand what this type is for, so it
    /// is worth writing for that audience.
    /// </summary>
    public virtual string? Description { get; protected set; }

    /// <summary>
    /// The fields this type pulls in, ordered. A flat list, deliberately - Cms nested these under
    /// <c>EntryFieldTab</c>, but that grouping only described a back-office form's layout, and it made
    /// "what fields does this type have?" a two-level traversal (总体设计 §2.3).
    /// <para>
    /// Persisted as a JSON column rather than a table: these rows are only ever read as a whole,
    /// together with their type, and never queried across types.
    /// </para>
    /// </summary>
    public virtual IReadOnlyList<ContentTypeField> Fields => _fields;

    public virtual Guid? TenantId { get; protected set; }

    public virtual void SetName(string name)
    {
        var checkedName = Check.NotNullOrWhiteSpace(name, nameof(name), ContentTypeConsts.MaxNameLength);
        if (!IdentifierName.IsValid(checkedName))
        {
            throw new InvalidValueFormatException(nameof(Name), checkedName);
        }

        Name = checkedName;
    }

    public virtual void SetDisplayName(string displayName)
    {
        DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), ContentTypeConsts.MaxDisplayNameLength);
    }

    public virtual void SetDescription(string? description)
    {
        Description = description;
    }

    /// <summary>
    /// Replaces the field arrangement wholesale, sorted by <see cref="ContentTypeField.Order"/>.
    /// <para>
    /// Rejects a field referenced twice. It would otherwise be silently harmless here and confusing
    /// everywhere downstream: both usages resolve against the same bag key, so the value would appear
    /// twice with possibly different Required flags, and the query index would get duplicate rows for
    /// one value.
    /// </para>
    /// </summary>
    public virtual void SetFields(IEnumerable<ContentTypeField> fields)
    {
        var list = fields.ToList();

        var duplicate = list.GroupBy(f => f.FieldId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new ContentTypeFieldDuplicatedException(duplicate.Key);
        }

        _fields = list.OrderBy(f => f.Order).ToList();
    }

    /// <summary>The ids of every field this type pulls in - what a bulk definition load is keyed by.</summary>
    public virtual IReadOnlyList<Guid> GetFieldIds()
    {
        return _fields.Select(f => f.FieldId).ToList();
    }
}
