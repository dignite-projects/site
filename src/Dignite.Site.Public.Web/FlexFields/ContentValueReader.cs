using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Dignite.Site.Public.FlexFields;

/// <summary>
/// Reads a <c>Content</c> field's value - a flat list of referenced Content ids - leniently: a fresh
/// in-memory value is a <see cref="List{Guid}"/> or a bare scalar, one that has round-tripped through
/// JSON storage is a <see cref="JsonElement"/> array of strings. Mirrors the shape-handling every other
/// field type's own lenient reader already does (<c>FieldTypeBase.ReadStringList</c>) - this project can't reach
/// <c>Dignite.FlexFields.Site.Content.ContentFieldType</c>'s own private reader, so it gets its own, same
/// as every other <c>.Web</c>-side reader does.
/// </summary>
internal static class ContentValueReader
{
    public static IReadOnlyList<Guid> ReadIds(object? value)
    {
        return ReadRaw(value)
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }

    private static List<string> ReadRaw(object? value)
    {
        switch (value)
        {
            case null:
                return new List<string>();
            case string single:
                return new List<string> { single };
            case JsonElement element:
                return ReadJson(element);
            case IEnumerable items:
                return items
                    .Cast<object?>()
                    .Where(item => item != null)
                    .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)!)
                    .ToList();
            default:
                return new List<string> { Convert.ToString(value, CultureInfo.InvariantCulture)! };
        }
    }

    private static List<string> ReadJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => new List<string>(),
            JsonValueKind.Array => element.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToList(),
            JsonValueKind.String => new List<string> { element.GetString()! },
            _ => new List<string>()
        };
    }
}
