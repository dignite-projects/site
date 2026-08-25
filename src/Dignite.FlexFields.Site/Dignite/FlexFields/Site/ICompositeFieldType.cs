using System.Collections.Generic;
using Dignite.Abp.FlexFields;

namespace Dignite.FlexFields.Site;

/// <summary>
/// A field type whose <i>configuration</i> declares further fields inline - <c>Matrix</c> (a block
/// type's sub-fields) and <c>Table</c> (the shared column schema). The kernel has no concept of this:
/// to <see cref="IFieldType"/> a configuration is an opaque bag, so "does a field of this type contain
/// other fields" has nowhere else to be asked.
///
/// <para>
/// <b>Why an interface rather than a bool.</b> Every caller that cares about compositeness also needs to
/// walk the nested fields - measuring nesting depth
/// (<see cref="CompositeFieldNesting"/>), validating recursively, describing a schema. A bare
/// <c>IsComposite</c> flag would leave each of those to switch on the concrete type to get at the
/// inline fields, which is exactly the coupling this replaces.
/// </para>
///
/// <para>
/// Lives here, not in the FlexFields kernel, because both composite types do: nothing in
/// <c>Dignite.Abp.FlexFields</c> declares fields inside a configuration. If a third composite type is
/// ever added, implementing this is what makes the nesting limit and the client's type picker account
/// for it automatically.
/// </para>
/// </summary>
public interface ICompositeFieldType : IFieldType
{
    /// <summary>
    /// The fields this type's configuration declares inline, flattened - <c>Matrix</c> returns every
    /// block type's fields together, since nesting depth and type-picker eligibility do not care which
    /// block a sub-field belongs to.
    /// </summary>
    IEnumerable<InlineFieldDefinition> GetInlineFields(FieldConfigurationDictionary configuration);
}
