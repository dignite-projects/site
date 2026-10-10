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
public class FileFieldType : FieldTypeBase
{
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

    public override FlexFieldValueType? IndexValueType => null;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();

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
