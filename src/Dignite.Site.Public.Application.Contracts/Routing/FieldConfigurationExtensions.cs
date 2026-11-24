using System.Collections.Generic;
using Dignite.Abp.FlexFields;

// Deliberately its own namespace, not Dignite.Site.Public.Routing (which Dignite.Site.Public.Application's
// own Routing/ files - e.g. RoutingPublicAppService - already occupy): C# resolves an extension method against
// the caller's own namespace before any using directive, so a same-name namespace here would silently take the
// calls written against Common.Application's ToFieldConfiguration.
namespace Dignite.Site.Public.Application.Contracts.Routing;

public static class FieldConfigurationExtensions
{
    /// <summary>
    /// <see cref="Dignite.Site.Fields.FieldDto.Configuration"/> is <c>IDictionary&lt;string, object?&gt;</c>;
    /// <see cref="FieldConfigurationDictionary"/> (<c>Dictionary&lt;string, object&gt;</c>, non-nullable
    /// value) can't take it directly - null-valued entries are dropped rather than coerced, which matches
    /// how <c>FieldConfigurationDictionaryExtensions.GetConfiguration</c> already treats "key absent" and
    /// "key present but null" identically. A null <paramref name="source"/> yields an empty result rather
    /// than throwing, matching <c>Dignite.Site.Common.FlexFieldValueDictionaryExtensions.ToFieldConfiguration</c>
    /// - the sibling this shares its contract with (that one lives in <c>Dignite.Site.Common.Application</c>,
    /// which <c>Dignite.Site.Public.Web</c> cannot reference, so this Contracts-tier copy exists instead of a
    /// direct reference to it).
    /// </summary>
    public static FieldConfigurationDictionary ToFieldConfiguration(this IDictionary<string, object?>? source)
    {
        var result = new FieldConfigurationDictionary();

        if (source == null)
        {
            return result;
        }

        foreach (var pair in source)
        {
            if (pair.Value is not null)
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }
}
