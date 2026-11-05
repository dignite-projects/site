using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using global::Scriban;
using global::Scriban.Parsing;
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
    /// <summary>
    /// Template data to JSON - the way back, for what a template hands to Site as JSON (the structured data of
    /// <c>site_head</c>): objects become <see cref="JsonObject"/> with their keys as the template wrote them, lists
    /// <see cref="JsonArray"/>, and strings, booleans, numbers and dates their JSON values. Anything else is written
    /// as the template would write it, as a string.
    /// </summary>
    public static JsonNode? ToJsonNode(TemplateContext context, SourceSpan span, object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return JsonValue.Create(text);
            case ScribanRawHtml html:
                return JsonValue.Create(html.Value);
            case bool flag:
                return JsonValue.Create(flag);
            case int number:
                return JsonValue.Create(number);
            case long number:
                return JsonValue.Create(number);
            case decimal number:
                return JsonValue.Create(number);
            case double number:
                return JsonValue.Create(number);
            case float number:
                return JsonValue.Create(number);
            case DateTime time:
                return JsonValue.Create(time);
            case DateTimeOffset time:
                return JsonValue.Create(time);
            // Before IScriptObject: a ScriptArray is one too, with members of its own (size, ...).
            case ScriptArray array:
            {
                var result = new JsonArray();
                foreach (var item in array)
                {
                    result.Add(ToJsonNode(context, span, item));
                }

                return result;
            }
            case IScriptObject obj:
            {
                var result = new JsonObject();
                foreach (var name in obj.GetMembers())
                {
                    obj.TryGetValue(context, span, name, out var member);
                    result[name] = ToJsonNode(context, span, member);
                }

                return result;
            }
            case IEnumerable items:
            {
                var result = new JsonArray();
                foreach (var item in items)
                {
                    result.Add(ToJsonNode(context, span, item));
                }

                return result;
            }
            default:
                return JsonValue.Create(context.ObjectToString(value));
        }
    }
}
