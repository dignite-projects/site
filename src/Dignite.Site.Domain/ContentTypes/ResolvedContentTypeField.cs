using Dignite.Site.Fields;

namespace Dignite.Site.ContentTypes;

/// <summary>
/// One field of a content type with both halves of its description present: the <b>definition</b> from
/// the tenant's field library and the <b>usage</b> from this particular content type (总体设计 §2.3).
/// <para>
/// The two are stored apart on purpose - <c>ContentTypeField</c> carries only a <c>FieldId</c> plus the
/// per-usage flags, while the name, type and configuration live on <see cref="Fields.Field"/> - so
/// anything that needs the whole picture has to join them. This is the joined pair, and
/// <see cref="ContentTypeFieldResolver"/> is the one place the join happens.
/// </para>
/// </summary>
public class ResolvedContentTypeField
{
    public ResolvedContentTypeField(Field definition, ContentTypeField usage)
    {
        Definition = definition;
        Usage = usage;
    }

    /// <summary>What the field intrinsically is: name (the value bag's key), type, configuration.</summary>
    public Field Definition { get; }

    /// <summary>How <i>this</i> content type uses it: Required, Searchable, ordering, display override.</summary>
    public ContentTypeField Usage { get; }

    /// <summary>
    /// The name this usage shows under - the usage's override if it set one, otherwise the definition's
    /// own. Note this is the <i>display</i> name only; <see cref="Fields.Field.Name"/> is never
    /// overridable, because it is the key values are stored under.
    /// </summary>
    public string DisplayName => Usage.DisplayName ?? Definition.DisplayName;
}
