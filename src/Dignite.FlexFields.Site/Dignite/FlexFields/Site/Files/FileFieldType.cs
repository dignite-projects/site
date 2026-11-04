using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site.Localization;

namespace Dignite.FlexFields.Site.Files;

/// <summary>
/// The file field type: one or more files picked from Site's file library by the admin UI's file picker
/// (<c>@dignite/ng.site</c>'s file field). It came from <c>Dignite.Abp.FlexFields</c> together with the file
/// library itself, and keeps that module's registration key, <see cref="ControlName"/> - a persisted value
/// in every field definition, Matrix block configurations included.
///
/// <para>
/// <b>Not indexable on purpose.</b> The stored value is what the picker hands back - an array of file
/// descriptor objects (id, container, blob name, name, MIME type, size, url) - not a scalar or a list of
/// scalars, so there is nothing sensible to put in a typed index column; the same reason RichText or Matrix
/// return <c>null</c> here.
/// </para>
///
/// <para>
/// <b>No reference to Site's domain.</b> This project references only <c>Dignite.Abp.FlexFields.Abstractions</c>,
/// like the Content and Seo field types, so it validates only what it can see - presence, per
/// <c>Required</c>. Whether a picked file exists is the picker's job at pick time and the public read
/// endpoint's at fetch time.
/// </para>
/// </summary>
public class FileFieldType : FieldTypeBase, IHasValueShape
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The registration key. <b>Persisted data</b> (<c>SiteFields.FieldTypeName</c>, Matrix block
    /// configurations) - unchanged from when the field type was <c>Dignite.Abp.FlexFields</c>'.
    /// </summary>
    public const string ControlName = "FileExplorer";

    public FileFieldType()
    {
        LocalizationResource = typeof(FlexFieldsSiteResource);
    }

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:File"];

    private static readonly FieldConfigurationKey[] Keys =
    {
        new(FileFieldConfigurationNames.FileContainerName, FieldConfigurationValueKind.String)
        {
            Required = true,
            Description = "The file container the value's files are uploaded to and picked from, one of the names " +
                          "site_list_file_containers gives, e.g. images."
        },
        new(FileFieldConfigurationNames.UploadFileMultiple, FieldConfigurationValueKind.Boolean)
        {
            DefaultValue = new FileFieldConfiguration().UploadFileMultiple,
            Description = "Whether the value is a list of files rather than one."
        }
    };

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys => Keys;

    /// <summary>
    /// The value is an array of file objects - an array even for one file - and the objects are what the file tools
    /// return (<c>site_upload_file</c>, <c>site_get_file</c>, the response of an upload link) or what the picker stores.
    /// The properties are named <c>[].key</c>, the key of each element. Plain English, like the configuration keys'
    /// descriptions: it documents a wire shape for a client choosing what to write.
    /// </summary>
    public IReadOnlyList<FieldValueShapeProperty> ValueShape => new[]
    {
        new FieldValueShapeProperty { Name = "[].url", Type = "string", Description = "Required: the file's address, as the file tools return it. A template reads the first file's url." },
        new FieldValueShapeProperty { Name = "[].id", Type = "string (GUID)", Description = "The file's id in the library." },
        new FieldValueShapeProperty { Name = "[].name", Type = "string", Description = "The file name." },
        new FieldValueShapeProperty { Name = "[].containerName", Type = "string", Description = "The container the file is in." },
        new FieldValueShapeProperty { Name = "[].blobName", Type = "string", Description = "The name the file is stored under." },
        new FieldValueShapeProperty { Name = "[].mimeType", Type = "string", Description = "The type detected from the file's bytes." },
        new FieldValueShapeProperty { Name = "[].size", Type = "integer", Description = "The size in bytes." },
    };

    public override FlexFieldValueType? IndexValueType => null;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();

        if (!IsFileValue(args.Field.Value))
        {
            errors.Add(
                new ValidationResult(
                    L["Validate:InvalidFileValue", args.Field.DisplayName],
                    new[] { args.Field.Name }
                    ));
            return errors;
        }

        if (args.Field.Required && !HasAnyValue(args.Field.Value))
        {
            errors.Add(
                new ValidationResult(
                    L["Validate:Required", args.Field.DisplayName],
                    new[] { args.Field.Name }
                    ));
        }

        return errors;
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new FileFieldConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Whether the value is something this field type stores: nothing (null, an empty string, an empty list), a file
    /// object, or a list of them - an object is a file when it has an address, a <c>url</c> that is a non-empty string,
    /// which is what a template reads. A plain string, a number or a list of strings is not: it used to be stored and
    /// then rendered as no file at all. Storage-shape-agnostic, like <see cref="HasAnyValue"/>: a live CLR value is
    /// read as the JSON it would be stored as.
    /// </summary>
    private static bool IsFileValue(object? value)
    {
        switch (value)
        {
            case null:
                return true;
            case string text:
                return string.IsNullOrEmpty(text);
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value, WebOptions);

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.Object => IsFile(element),
            JsonValueKind.Array => element.EnumerateArray().All(IsFile),
            _ => false,
        };

        static bool IsFile(JsonElement file)
        {
            return file.ValueKind == JsonValueKind.Object
                   && file.TryGetProperty("url", out var url)
                   && url.ValueKind == JsonValueKind.String
                   && !string.IsNullOrWhiteSpace(url.GetString());
        }
    }

    /// <summary>
    /// Storage-shape-agnostic presence check, in the spirit of <see cref="FieldTypeBase.ReadStringList"/>
    /// but for a value that is a list of objects: a fresh in-memory value is some <see cref="IEnumerable"/>,
    /// a value that has round-tripped through JSON is a <see cref="JsonElement"/>.
    /// </summary>
    private static bool HasAnyValue(object? value)
    {
        switch (value)
        {
            case null:
                return false;
            // Before IEnumerable: a string is a sequence of chars. This field type never stores one, but a
            // stray string value should still read as "present" rather than "a sequence of chars".
            case string str:
                return !string.IsNullOrEmpty(str);
            case JsonElement element:
                return element.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => false,
                    JsonValueKind.Array => element.GetArrayLength() > 0,
                    _ => true,
                };
            case IEnumerable enumerable:
                return enumerable.Cast<object?>().Any();
            default:
                return true;
        }
    }
}
