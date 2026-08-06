using System.Collections.Generic;

namespace Dignite.Site.Admin.Schema;

/// <summary>
/// One field as it appears in a content type: the library <b>definition</b> merged with <b>this type's
/// usage</b> of it (总体设计 §2.3, §6.2.4).
/// <para>
/// The two halves are stored apart and both matter to a caller: <see cref="FieldTypeName"/> and
/// <see cref="Configuration"/> say what a valid value looks like, while <see cref="Required"/> and
/// <see cref="Order"/> are this type's own and can differ for the same field in another type.
/// </para>
/// </summary>
public class SiteSchemaFieldDto
{
    /// <summary>
    /// The definition's name - and therefore <b>the key to use in a write's field-value bag</b>. Not
    /// overridable per usage, precisely because it is that key.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>This usage's display name if it overrides one, otherwise the definition's.</summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>
    /// What to put in this field, written for an AI client to read. Alongside the content type's own
    /// description, this is the entire briefing a model gets (总体设计 §6.2.6) - including for the
    /// <c>seo</c> preset, which is why SEO metadata is filled in while writing the body rather than as a
    /// separate step (issue #27).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>The FlexFields field type this field is bound to, e.g. <c>TextEdit</c>.</summary>
    public string FieldTypeName { get; set; } = default!;

    /// <summary>
    /// Type-specific configuration - length limits, the option list of a select, and so on. This is what
    /// a write is validated against, so it is also what a value has to be generated to fit.
    /// </summary>
    public IDictionary<string, object?> Configuration { get; set; } = new Dictionary<string, object?>();

    /// <summary>Whether a value is required for this field <i>in this content type</i>.</summary>
    public bool Required { get; set; }

    /// <summary>Whether this usage's value is decomposed into the query index (总体设计 §2.4).</summary>
    public bool Searchable { get; set; }

    /// <summary>Position within this content type.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether list views surface this field.
    /// <para>
    /// Present for a blunt reason: <c>update_content_type</c> replaces a type's whole field arrangement
    /// and tells the caller to read the current one from here and send it back with its changes merged
    /// in. Any usage flag missing from this document is therefore silently reset to its default on every
    /// such edit, so the schema has to carry every flag the write side accepts - not merely the ones a
    /// reader finds interesting.
    /// </para>
    /// </summary>
    public bool ShowInList { get; set; }

    /// <summary>
    /// The schema.org property this usage maps to, e.g. <c>headline</c> (总体设计 §5.4). Null means the
    /// field plays no part in the content type's JSON-LD. Round-trips for the same reason as
    /// <see cref="ShowInList"/> - without it, editing a content type would quietly strip the JSON-LD
    /// mapping off every one of its fields.
    /// </summary>
    public string? SchemaProperty { get; set; }
}
