namespace Dignite.FlexFields.Site;

/// <summary>
/// One key of a composite field type's value, as declared by <see cref="IHasValueShape"/> - the wire
/// name, its JSON type, and what belongs in it.
/// </summary>
public class FieldValueShapeProperty
{
    /// <summary>The key as it appears on the wire, e.g. <c>metaTitle</c> - always camelCase.</summary>
    public string Name { get; set; } = default!;

    /// <summary>The JSON type expected under this key: <c>string</c>, <c>boolean</c>, <c>number</c> or <c>object</c>.</summary>
    public string Type { get; set; } = default!;

    /// <summary>What belongs in this key, written for an AI client to read.</summary>
    public string? Description { get; set; }
}
