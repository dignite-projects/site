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
    /// Whether this type's configuration declares further fields inline - <c>Matrix</c> and <c>Table</c>,
    /// both flex-fields kernel built-ins as of 10.0.0-rc.16 (<c>Dignite.Abp.FlexFields.ICompositeFieldType</c>;
    /// used to be Site's own copy of that interface, before the port). Populated from that same check,
    /// not restated here, so a third composite type added later needs nothing changed on this side.
    /// <para>
    /// Currently consumed by <c>Dignite.Site.Mcp.Fields.FieldTools.ListFieldTypesAsync</c>, which hands
    /// this whole DTO straight to AI clients so they know which field types need a composite (list of
    /// sub-objects) value rather than a scalar - not by the Angular admin UI, which used to read this
    /// to filter its Matrix/Table config editors' type picker but no longer needs to: the kernel's own
    /// <c>ff-matrix-config</c>/<c>ff-table-config</c> components read <c>composite</c> directly off
    /// <c>FieldTypeResolver.getAll()</c> synchronously instead of round-tripping through Site's admin API.
    /// </para>
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
