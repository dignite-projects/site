using System.Collections.Generic;
using System.Linq;
using Dignite.Abp.FlexFields;

namespace Dignite.Site.Common;

/// <summary>
/// Converts between the FlexFields kernel's own dictionaries (<c>FlexFieldDictionary</c> on
/// <c>Content.FlexFields</c>, <c>FieldConfigurationDictionary</c> on <c>Field.Configuration</c> - both a
/// <c>Dictionary&lt;string, object&gt;</c> underneath) and the plain <c>IDictionary&lt;string, object?&gt;</c>
/// the application contracts expose, so neither kernel type ever crosses into a DTO.
/// <para>
/// Hand-written rather than left to Mapperly: <c>Dictionary&lt;string, object&gt;</c> and
/// <c>IDictionary&lt;string, object?&gt;</c> are different closed generic types to the compiler even
/// though they hold the same values at runtime, because generic collections are invariant in C#. The
/// FlexFields demo hits the same wall and hand-writes the identical conversion for the same reason.
/// </para>
/// <para>
/// Shared here (rather than duplicated in Admin and Public) because it is a plain static method, not a
/// Mapperly-registered mapper - it carries none of the per-module DI-scanning restriction that keeps the
/// entity-to-DTO mappers themselves one copy per application module.
/// </para>
/// </summary>
public static class FlexFieldValueDictionaryExtensions
{
    public static Dictionary<string, object?> ToValueDictionary(this Dictionary<string, object> source)
    {
        return source.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
    }

    /// <summary>
    /// The write direction, used only for <c>Field.Configuration</c> - <c>Content.FlexFields</c> never
    /// needs it, since <c>ContentManager.CreateAsync</c>/<c>UpdateAsync</c> already accept a plain
    /// <c>IDictionary&lt;string, object?&gt;</c> directly. A null value is dropped rather than stored,
    /// matching <c>SetField</c>'s own "null clears the key" contract.
    /// </summary>
    public static FieldConfigurationDictionary ToFieldConfiguration(this IDictionary<string, object?>? source)
    {
        var result = new FieldConfigurationDictionary();

        if (source == null)
        {
            return result;
        }

        foreach (var (key, value) in source)
        {
            if (value != null)
            {
                result[key] = value;
            }
        }

        return result;
    }
}
