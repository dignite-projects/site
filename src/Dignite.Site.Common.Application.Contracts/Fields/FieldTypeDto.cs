namespace Dignite.Site.Fields;

/// <summary>
/// One <c>IFieldType</c> registered with the FlexFields kernel, surfaced so a caller can discover legal
/// <c>FieldTypeName</c> values before creating or editing a field. No display label - by the same
/// convention as the FlexFields demo, localization of the name is the client's job.
/// </summary>
public class FieldTypeDto
{
    public string Name { get; set; } = default!;

    /// <summary>Whether values of this type can be searched via a flex-field query condition.</summary>
    public bool Indexable { get; set; }
}
