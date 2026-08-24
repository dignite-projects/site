using System.Collections.Generic;
using System.Text.Json;
using Dignite.FlexFields.Site.Matrix;

namespace Dignite.Site.Public.FlexFields;

/// <summary>
/// Reads a <c>Matrix</c> field's value - a list of block instances - leniently: a fresh in-memory value
/// is a <see cref="List{MatrixBlockValue}"/>, one that has round-tripped through JSON storage is a
/// <see cref="JsonElement"/> array. This project can't reach
/// <c>Dignite.FlexFields.Site.Matrix.MatrixFieldType</c>'s own private reader, so it gets its own, same
/// as every other <c>.Web</c>-side reader does.
/// </summary>
internal static class MatrixValueReader
{
    public static IReadOnlyList<MatrixBlockValue> ReadBlocks(object? value)
    {
        return value switch
        {
            null => new List<MatrixBlockValue>(),
            List<MatrixBlockValue> list => list,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => new List<MatrixBlockValue>(),
            JsonElement { ValueKind: JsonValueKind.Array } element =>
                element.Deserialize<List<MatrixBlockValue>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new List<MatrixBlockValue>(),
            _ => new List<MatrixBlockValue>()
        };
    }
}
