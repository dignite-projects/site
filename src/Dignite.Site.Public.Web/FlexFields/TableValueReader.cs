using System.Collections.Generic;
using System.Text.Json;
using Dignite.FlexFields.Site.Table;

namespace Dignite.Site.Public.FlexFields;

/// <summary>
/// Reads a <c>Table</c> field's value - a list of rows - leniently: a fresh in-memory value is a
/// <see cref="List{TableRow}"/>, one that has round-tripped through JSON storage is a
/// <see cref="JsonElement"/> array. This project can't reach
/// <c>Dignite.FlexFields.Site.Table.TableFieldType</c>'s own private reader, so it gets its own, same as
/// every other <c>.Web</c>-side reader does.
/// </summary>
internal static class TableValueReader
{
    public static IReadOnlyList<TableRow> ReadRows(object? value)
    {
        return value switch
        {
            null => new List<TableRow>(),
            List<TableRow> list => list,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => new List<TableRow>(),
            JsonElement { ValueKind: JsonValueKind.Array } element =>
                element.Deserialize<List<TableRow>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new List<TableRow>(),
            _ => new List<TableRow>()
        };
    }
}
