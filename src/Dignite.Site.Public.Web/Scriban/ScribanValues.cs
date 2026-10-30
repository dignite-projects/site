using System.Text.Json;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// JSON to template data: objects become <see cref="ScriptObject"/>, arrays <see cref="ScriptArray"/>, and the
/// rest strings, booleans, numbers or null - never a CLR object a template could reflect on.
/// <para>
/// Scriban's own <c>Import(JsonElement)</c> covers an object root only and turns every number into a
/// <see cref="decimal"/>; a field value is as often an array or a scalar, and an integer is more useful as an
/// integer (a range bound, an index), so numbers are <see cref="int"/>, then <see cref="long"/>, then
/// <see cref="decimal"/>, then <see cref="double"/>, whichever holds them first.
/// </para>
/// </summary>
public static class ScribanValues
{
    public static object? FromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new ScriptObject();
                foreach (var property in element.EnumerateObject())
                {
                    obj.SetValue(property.Name, FromJson(property.Value), false);
                }
                return obj;
            case JsonValueKind.Array:
                var array = new ScriptArray();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(FromJson(item));
                }
                return array;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var int32))
                {
                    return int32;
                }
                if (element.TryGetInt64(out var int64))
                {
                    return int64;
                }
                if (element.TryGetDecimal(out var number))
                {
                    return number;
                }
                return element.GetDouble();
            default:
                return null;
        }
    }

    /// <summary><see cref="FromJson"/> for a value that serializes to a JSON object; an empty object otherwise.</summary>
    public static ScriptObject ObjectFromJson(JsonElement element)
    {
        return FromJson(element) as ScriptObject ?? new ScriptObject();
    }
}
