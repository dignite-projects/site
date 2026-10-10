using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Dignite.Site.Public.FlexFields;

/// <summary>
/// Reads a file field's value (registration key <c>FileExplorer</c>, see <c>FileFieldType</c>) - what the admin
/// UI's file picker denormalizes at pick time: an array of file descriptor objects
/// (id/containerName/blobName/name/mimeType/size/url, ...; the admin API's <c>FileDescriptorDto</c>) -
/// leniently: a fresh in-memory value is a
/// <see cref="IEnumerable"/> of dictionary-like objects, one that has round-tripped through JSON
/// storage is a <see cref="JsonElement"/> array of objects. Property names are read as stored -
/// <c>Dictionary&lt;string, object?&gt;</c> serializes its keys literally, so whatever casing the value
/// was written with (this project reads the same camelCase the Angular <c>FileDescriptorDto</c> uses)
/// round-trips unchanged; there is no reflection-based naming policy in between to second-guess.
/// </summary>
internal static class FileValueReader
{
    public static IReadOnlyList<FileView> ReadFiles(object? value)
    {
        return value switch
        {
            null => new List<FileView>(),
            JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Select(ReadJsonFile).ToList(),
            JsonElement { ValueKind: JsonValueKind.Object } single => new List<FileView> { ReadJsonFile(single) },
            JsonElement => new List<FileView>(),
            IDictionary<string, object?> singleDictionary => new List<FileView> { ReadDictionaryFile(singleDictionary) },
            IEnumerable items => items
                .Cast<object?>()
                .Select(ReadObjectFile)
                .Where(file => file != null)
                .Select(file => file!)
                .ToList(),
            _ => new List<FileView>()
        };
    }

    private static FileView? ReadObjectFile(object? item)
    {
        return item switch
        {
            JsonElement element => ReadJsonFile(element),
            IDictionary<string, object?> dictionary => ReadDictionaryFile(dictionary),
            _ => null
        };
    }

    private static FileView ReadJsonFile(JsonElement element)
    {
        return new FileView(
            Name: GetString(element, "name") ?? GetString(element, "blobName") ?? "file",
            Size: GetLong(element, "size"),
            MimeType: GetString(element, "mimeType"),
            Url: GetString(element, "url"));
    }

    private static FileView ReadDictionaryFile(IDictionary<string, object?> dictionary)
    {
        return new FileView(
            Name: GetString(dictionary, "name") ?? GetString(dictionary, "blobName") ?? "file",
            Size: GetLong(dictionary, "size"),
            MimeType: GetString(dictionary, "mimeType"),
            Url: GetString(dictionary, "url"));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value)
            ? value
            : null;
    }

    private static string? GetString(IDictionary<string, object?> dictionary, string propertyName)
    {
        return dictionary.TryGetValue(propertyName, out var value) ? value as string : null;
    }

    private static long? GetLong(IDictionary<string, object?> dictionary, string propertyName)
    {
        if (!dictionary.TryGetValue(propertyName, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            long l => l,
            int i => i,
            _ => null
        };
    }
}

/// <summary>Just enough of a file descriptor for the default view to render - name, size, MIME type and
/// a link. Anything richer (thumbnails, upload date, ...) is a reason to override this view, not to
/// grow this shape.</summary>
internal record FileView(string Name, long? Size, string? MimeType, string? Url);
