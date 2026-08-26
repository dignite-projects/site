using System.Collections.Generic;

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

    /// <summary>
    /// Whether this type's configuration declares further fields inline - <c>Matrix</c> and <c>Table</c>.
    /// Served for the same reason <see cref="Indexable"/> is: the designer has to stop offering composite
    /// types once <c>CompositeFieldNesting.MaxDepth</c> is reached, and which types those are is the
    /// server's answer (<c>ICompositeFieldType</c>), not a list for the client to hand-maintain.
    /// </summary>
    public bool Composite { get; set; }

    /// <summary>
    /// The keys of this type's value, for a field type whose value is a fixed composite object rather
    /// than a scalar - null for every other field type, including a composite one like <c>Matrix</c> whose
    /// sub-fields already vary per field instance and are described in that field's own <c>Configuration</c>
    /// instead. Populated from <c>IHasValueShape</c> when the field type implements it - see that
    /// interface for why this is a type-level fact served here rather than duplicated into every field's
    /// <c>Configuration</c>.
    /// </summary>
    public IReadOnlyList<FieldValuePropertyDto>? ValueShape { get; set; }
}

/// <summary>
/// One key of <see cref="FieldTypeDto.ValueShape"/> - the contract-side mirror of
/// <c>Dignite.FlexFields.Site.FieldValueShapeProperty</c>, kept separate so this project never takes a
/// dependency on the FlexFields kernel or Site's own field-type library (same reasoning as
/// <see cref="FieldDto.Configuration"/> being a plain dictionary rather than the kernel's own type).
/// </summary>
public class FieldValuePropertyDto
{
    public string Name { get; set; } = default!;

    public string Type { get; set; } = default!;

    public string? Description { get; set; }
}
