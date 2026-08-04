using System;
using Dignite.Abp.FlexFields;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Sites.Fields;

/// <summary>
/// One definition in the tenant's field library: what a field <i>is</i>, independent of where it is used
/// (总体设计 §2.3).
/// <para>
/// This is Sites' implementation of FlexFields' <c>IFlexField</c> - the whole field mechanism (types,
/// configuration, validation, the value bag, the query index, rename migration) comes from that kernel,
/// and this entity plus <c>Content</c>'s bag plus one provider is the entire surface Sites has to write
/// for it (§8.2). Everything <c>IFlexField</c> declares is mapped by the kernel's
/// <c>ConfigureFlexField&lt;Field&gt;()</c>, including the column lengths; <see cref="GroupId"/> is the
/// one property Sites adds on top, and the one Sites maps itself.
/// </para>
/// <para>
/// Defined once, used anywhere: a <c>title</c> field is defined here and pulled into the article,
/// gallery and video content types alike. That is the payoff of a separate definition table over fields
/// inlined in each type - widening its character limit is one edit here, and every type that uses it
/// follows.
/// </para>
/// <para>
/// An aggregate root because <c>IFlexField</c> requires it (<c>IAggregateRoot&lt;Guid&gt;</c>), which is
/// also the honest modelling: a definition's lifetime is independent of any content type that
/// references it. Note that Dignite.Cms's <c>Field</c> was a plain entity.
/// </para>
/// </summary>
public class Field : FullAuditedAggregateRoot<Guid>, IFlexField, IMultiTenant
{
    protected Field()
    {
    }

    public Field(
        Guid id,
        string name,
        string displayName,
        string fieldTypeName,
        string? description = null,
        FieldConfigurationDictionary? configuration = null,
        Guid? groupId = null,
        Guid? tenantId = null)
        : base(id)
    {
        SetName(name);
        SetDisplayName(displayName);
        SetFieldTypeName(fieldTypeName);
        SetDescription(description);
        SetGroupId(groupId);
        Configuration = configuration ?? new FieldConfigurationDictionary();
        TenantId = tenantId;
    }

    /// <summary>
    /// Unique within the tenant, e.g. <c>title</c>, <c>body</c>, <c>cover</c>.
    /// <para>
    /// This is <b>also the key the value is stored under</b> in every content's value bag, which is why
    /// renaming a field is a data migration rather than an edit - see <see cref="FieldManager"/> for the
    /// ordering that has to be followed. The query index does not share this problem: its rows key on
    /// the field's id, so a rename leaves them untouched. The two layers key differently on purpose
    /// (总体设计 §2.4).
    /// </para>
    /// </summary>
    public virtual string Name { get; protected set; } = default!;

    public virtual string DisplayName { get; protected set; } = default!;

    /// <summary>
    /// Doubles as the field description handed to an AI client over MCP - "schema is the contract"
    /// (总体设计 §1.2), and this is the part of the schema that explains itself.
    /// </summary>
    public virtual string? Description { get; protected set; }

    /// <summary>
    /// Registration name of the FlexFields <c>IFieldType</c> this field is bound to. Six ship with the
    /// kernel (<c>TextEdit</c>, <c>NumericEdit</c>, <c>DateEdit</c>, <c>Select</c>, <c>Switch</c>,
    /// <c>TreeView</c>); Sites registers its own extras through DI without touching the kernel.
    /// </summary>
    public virtual string FieldTypeName { get; protected set; } = default!;

    /// <summary>Type-specific configuration, interpreted by the field type.</summary>
    public virtual FieldConfigurationDictionary Configuration { get; protected set; } = new();

    /// <summary>
    /// Optional library grouping - Sites' own, not part of <c>IFlexField</c>. Purely organizational
    /// (总体设计 §2.3): it makes a large field library navigable and has no effect at runtime.
    /// </summary>
    public virtual Guid? GroupId { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// Renames the definition. Callers must not use this directly - go through
    /// <see cref="FieldManager.RenameAsync"/>, which also rewrites the value bags this name keys into.
    /// Calling it alone orphans every stored value under the old key.
    /// </summary>
    public virtual void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), FlexFieldConsts.MaxNameLength);
    }

    public virtual void SetDisplayName(string displayName)
    {
        DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), FlexFieldConsts.MaxDisplayNameLength);
    }

    public virtual void SetDescription(string? description)
    {
        Check.Length(description, nameof(description), FlexFieldConsts.MaxDescriptionLength);
        Description = description;
    }

    public virtual void SetFieldTypeName(string fieldTypeName)
    {
        FieldTypeName = Check.NotNullOrWhiteSpace(
            fieldTypeName, nameof(fieldTypeName), FlexFieldConsts.MaxFieldTypeNameLength);
    }

    public virtual void SetConfiguration(FieldConfigurationDictionary? configuration)
    {
        Configuration = configuration ?? new FieldConfigurationDictionary();
    }

    /// <summary>
    /// Assigns the library group. <see cref="Guid.Empty"/> is treated as "no group" - it is what an
    /// unset value arrives as from a form post or a JSON payload, and storing it would leave a foreign
    /// key pointing at a group that cannot exist.
    /// </summary>
    public virtual void SetGroupId(Guid? groupId)
    {
        GroupId = groupId.HasValue && groupId.Value != Guid.Empty ? groupId : null;
    }
}
