using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields.Select;
using Dignite.Site.Contents;
using Dignite.Site.Public.Routing;

namespace Dignite.Site.Public.Templating;

/// <summary>
/// Reads a content's field values inside a template, by field name.
/// <para>
/// Field names belong to one site's content types, defined per tenant in the field library, so they are
/// written in that site's templates themselves - never collected into constants in a host's compiled
/// code, where a field added or renamed in the admin would need a rebuild of every site's host.
/// </para>
/// <para>
/// Values reach a template over HTTP in a host like Dignite.Cloud.Web.Public, so a value is usually a
/// <see cref="JsonElement"/>; in a single-process host it may still be a plain CLR value. Every reader
/// here accepts both, and a value of the wrong shape reads as empty rather than throwing - a template
/// renders what it can.
/// </para>
/// </summary>
public static class ContentTemplateExtensions
{
    /// <summary>
    /// A field's value as text: a string, a number or a boolean as written. A multi-value field (a
    /// multiple-choice Select, say) yields its first value - see <see cref="GetTexts(ContentDto, string)"/>.
    /// </summary>
    public static string? GetText(this ContentDto content, string fieldName)
    {
        return content.GetTexts(fieldName).FirstOrDefault();
    }

    /// <summary>Every value of a field as text, in stored order; empty when the field has none.</summary>
    public static IReadOnlyList<string> GetTexts(this ContentDto content, string fieldName)
    {
        return content.FieldValues.TryGetValue(fieldName, out var value) ? ReadTexts(value) : Array.Empty<string>();
    }

    /// <summary>The first file's address in a FileExplorer field (an array of file descriptors).</summary>
    public static string? GetFileUrl(this ContentDto content, string fieldName)
    {
        return content.FieldValues.TryGetValue(fieldName, out var value) ? ReadFileUrl(value) : null;
    }

    /// <summary>
    /// The blocks of a Matrix field, each as its sub-field values object - read those with
    /// <see cref="GetText(JsonElement, string)"/> and <see cref="GetFileUrl(JsonElement, string)"/>. A
    /// Matrix value is stored as <c>[{"blockTypeName": ..., "values": {...}}]</c>.
    /// </summary>
    public static IReadOnlyList<JsonElement> GetMatrixBlocks(this ContentDto content, string fieldName)
    {
        if (!content.FieldValues.TryGetValue(fieldName, out var value) || ToJson(value) is not { ValueKind: JsonValueKind.Array } blocks)
        {
            return Array.Empty<JsonElement>();
        }

        return blocks.EnumerateArray()
            .Select(block => block.ValueKind == JsonValueKind.Object && block.TryGetProperty("values", out var values) ? values : default)
            .Where(values => values.ValueKind == JsonValueKind.Object)
            .ToList();
    }

    /// <inheritdoc cref="GetText(ContentDto, string)"/>
    public static string? GetText(this ContentRenderViewModel content, string fieldName)
    {
        return content.Content.GetText(fieldName);
    }

    /// <inheritdoc cref="GetTexts(ContentDto, string)"/>
    public static IReadOnlyList<string> GetTexts(this ContentRenderViewModel content, string fieldName)
    {
        return content.Content.GetTexts(fieldName);
    }

    /// <inheritdoc cref="GetFileUrl(ContentDto, string)"/>
    public static string? GetFileUrl(this ContentRenderViewModel content, string fieldName)
    {
        return content.Content.GetFileUrl(fieldName);
    }

    /// <inheritdoc cref="GetMatrixBlocks(ContentDto, string)"/>
    public static IReadOnlyList<JsonElement> GetMatrixBlocks(this ContentRenderViewModel content, string fieldName)
    {
        return content.Content.GetMatrixBlocks(fieldName);
    }

    /// <summary>
    /// The options a Select field has chosen, value and label, in stored order - labels from the field's
    /// own definition in <see cref="ContentRenderViewModel.Fields"/>, so an option renamed in the admin
    /// shows its new label everywhere at once. A value no option has any more (removed from the field
    /// since this content was saved) is returned with the value itself as its label.
    /// </summary>
    public static IReadOnlyList<SelectListItem> GetSelectedOptions(this ContentRenderViewModel content, string fieldName)
    {
        var values = content.Content.GetTexts(fieldName);
        if (values.Count == 0)
        {
            return Array.Empty<SelectListItem>();
        }

        var field = content.Fields.FirstOrDefault(f => f.Name == fieldName);
        IReadOnlyList<SelectListItem> options = field == null
            ? Array.Empty<SelectListItem>()
            : new SelectConfiguration(field.Configuration).Options;

        return values
            .Select(value => new SelectListItem(options.FirstOrDefault(o => o.Value == value)?.Text ?? value, value, true))
            .ToList();
    }

    /// <summary>A text sub-field of a Matrix block's values - see <see cref="GetMatrixBlocks(ContentDto, string)"/>.</summary>
    public static string? GetText(this JsonElement blockValues, string fieldName)
    {
        return blockValues.ValueKind == JsonValueKind.Object && blockValues.TryGetProperty(fieldName, out var value)
            ? ReadTexts(value).FirstOrDefault()
            : null;
    }

    /// <summary>The first file's address in a FileExplorer sub-field of a Matrix block's values.</summary>
    public static string? GetFileUrl(this JsonElement blockValues, string fieldName)
    {
        return blockValues.ValueKind == JsonValueKind.Object && blockValues.TryGetProperty(fieldName, out var value)
            ? ReadFileUrl(value)
            : null;
    }

    private static IReadOnlyList<string> ReadTexts(object? value)
    {
        switch (value)
        {
            case null:
                return Array.Empty<string>();
            case string text:
                return new[] { text };
            case JsonElement element:
                return element.ValueKind switch
                {
                    JsonValueKind.String => new[] { element.GetString()! },
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => new[] { element.GetRawText() },
                    JsonValueKind.Array => element.EnumerateArray().SelectMany(item => ReadTexts(item)).ToList(),
                    _ => Array.Empty<string>()
                };
            case IEnumerable items:
                return items.Cast<object?>().SelectMany(ReadTexts).ToList();
            case bool flag:
                return new[] { flag ? "true" : "false" };
            default:
                return new[] { Convert.ToString(value, CultureInfo.InvariantCulture)! };
        }
    }

    /// <summary>
    /// A FileExplorer value is an array of file descriptor objects (<c>id</c>, <c>name</c>, <c>url</c>,
    /// ...); a single descriptor object is accepted too.
    /// </summary>
    private static string? ReadFileUrl(object? value)
    {
        if (ToJson(value) is not { } files)
        {
            return null;
        }

        var file = files.ValueKind == JsonValueKind.Array ? files.EnumerateArray().FirstOrDefault() : files;
        return file.ValueKind == JsonValueKind.Object && file.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String
            ? url.GetString()
            : null;
    }

    /// <summary>A value as JSON - as is when it already is, serialized when it is a plain CLR value.</summary>
    private static JsonElement? ToJson(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => element,
            _ => JsonSerializer.SerializeToElement(value)
        };
    }
}
