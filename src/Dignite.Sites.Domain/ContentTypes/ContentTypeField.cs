using System;

namespace Dignite.Sites.ContentTypes;

/// <summary>
/// One field pulled into a content type: a reference to a <c>Field</c> in the tenant's field library,
/// plus how <i>this</i> type uses it (总体设计 §2.3).
/// <para>
/// The split is the point. A <c>Field</c> describes what a field intrinsically is - its type, its
/// configuration - and that does not change between the types that use it. Required, Searchable,
/// ordering and the display-name override describe an intent that is specific to this usage: the same
/// <c>title</c> field can be required and searchable in <c>post-article</c> and optional and unindexed
/// somewhere else, and both are correct. Dignite.Cms's <c>EntryField</c> cuts it here too, and so does
/// FlexFields' <c>FlexFieldValue</c>, whose own docs call these flags "per-usage rather than
/// per-definition".
/// </para>
/// <para>
/// A value object, not an entity: it is stored as part of its content type's JSON column and has no
/// identity or lifetime of its own. Unlike Cms there is no <c>EntryFieldTab</c> above it - that grouping
/// only ever meant something to a back-office form, and Sites' primary write path is MCP (§2.3).
/// </para>
/// </summary>
[Serializable]
public class ContentTypeField
{
    protected ContentTypeField()
    {
    }

    public ContentTypeField(
        Guid fieldId,
        bool required = false,
        bool searchable = false,
        bool showInList = false,
        string? displayName = null,
        int order = 0)
    {
        FieldId = fieldId;
        Required = required;
        Searchable = searchable;
        ShowInList = showInList;
        DisplayName = displayName;
        Order = order;
    }

    /// <summary>The <c>Field.Id</c> this reference points at.</summary>
    public virtual Guid FieldId { get; protected set; }

    /// <summary>Whether a value is required when saving a content of this type.</summary>
    public virtual bool Required { get; protected set; }

    /// <summary>
    /// Whether this usage's value is decomposed into the query index (总体设计 §2.4). Only
    /// searchable usages produce index rows, and only if the field's type is indexable at all - rich
    /// text, for instance, has no <c>IndexValueType</c> and is skipped regardless.
    /// </summary>
    public virtual bool Searchable { get; protected set; }

    /// <summary>Whether list views surface this field.</summary>
    public virtual bool ShowInList { get; protected set; }

    /// <summary>Optional override of the field's own display name, for this type only.</summary>
    public virtual string? DisplayName { get; protected set; }

    /// <summary>Position within this content type.</summary>
    public virtual int Order { get; protected set; }

    /// <summary>
    /// Value equality, because this is a value object and because EF Core needs it: the field list is
    /// persisted through a value converter, and a converted collection with no way to compare its
    /// elements is compared by reference instead - which is how an edit to a content type's arrangement
    /// gets silently dropped. Paired with <c>ContentTypeFieldListValueComparer</c> in the EF Core layer.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is ContentTypeField other
               && FieldId.Equals(other.FieldId)
               && Required == other.Required
               && Searchable == other.Searchable
               && ShowInList == other.ShowInList
               && Order == other.Order
               && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FieldId, Required, Searchable, ShowInList, Order, DisplayName);
    }
}
