using System.Collections.Generic;

namespace Dignite.FlexFields.Site;

/// <summary>
/// A field type whose value is a fixed composite object - not admin-configured, unlike Matrix's block
/// types or Table's columns, which already describe their own sub-fields through
/// <see cref="Dignite.Abp.FlexFields.ICompositeFieldType"/> (a flex-fields kernel built-in as of
/// 10.0.0-rc.16) and are visible in a field's own <c>Configuration</c> because they genuinely vary per
/// field instance.
/// <para>
/// Seo's value shape does not vary - every Seo field has exactly the same four keys, fixed by
/// <c>SeoFieldValue</c> - so it does not belong in <c>Configuration</c> (which would mean storing the
/// same static fact redundantly on every field row, out of sync the moment the type gains a property).
/// It belongs on the type itself, read once from <c>list_field_types</c>/<c>FieldTypeDto</c> - the
/// existing catalog of type-level facts (<c>Indexable</c>, <c>Composite</c>) an AI client already knows
/// to consult when a field type's name alone is not enough (GitHub discussion on <c>SeoFieldValue</c>
/// vs. <c>site_schema</c> exposure).
/// </para>
/// </summary>
public interface IHasValueShape
{
    /// <summary>This field type's value, one property per key. Never null; empty only if the value has no keys at all.</summary>
    IReadOnlyList<FieldValueShapeProperty> ValueShape { get; }
}
