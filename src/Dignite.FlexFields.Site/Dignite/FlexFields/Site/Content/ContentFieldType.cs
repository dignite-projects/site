using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Dignite.Abp.FlexFields;

namespace Dignite.FlexFields.Site.Content;

/// <summary>
/// The <c>Content</c> field type: a reference to one or more Site <c>Content</c> records, picked through
/// an admin picker (optionally restricted to one content type via <see cref="ContentConfiguration.ContentTypeId"/>).
/// Semantically identical to DynamicForms' own <c>EntryFormControl</c> - a relation picker, not a nested
/// object; the value is always a flat list of ids, even when <see cref="ContentConfiguration.Multiple"/>
/// is false. Named "Content" rather than "Entry" (DynamicForms' own name): Site's domain has no "Entry"
/// concept at all, only <c>Content</c> - naming the field type after what it actually references reads
/// far more clearly than borrowing a CMS-specific term that has no counterpart here.
///
/// <para>
/// <b>Indexable, unlike Matrix.</b> The value decomposes into one or more <see cref="Guid"/>s - exactly
/// the shape <see cref="FlexFieldValueType.Guid"/> exists for - so this field type can be searched
/// ("contents referencing X"), the same multi-valued pattern <c>Select</c>/<c>Tree</c> already use.
/// </para>
///
/// <para>
/// <b>Zero reference to Dignite.Site.Domain.</b> Validating that a referenced id still exists, or that
/// <see cref="ContentConfiguration.ContentTypeId"/> names a real content type, would need this project to
/// depend on Site's own domain model - exactly what referencing only
/// <c>Dignite.Abp.FlexFields.Abstractions</c> rules out. So this type validates only what it can see:
/// presence, per <c>Required</c>. Anything deeper is the picker's job at pick time and the renderer's job
/// at display time (same precedent <c>FileExplorerFieldType</c> already set for its own out-of-reach
/// domain, <c>Dignite.FileExplorer</c>).
/// </para>
/// </summary>
public class ContentFieldType : FieldTypeBase
{
    public const string ControlName = "Content";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Content"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.Guid;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();
        var value = ReadGuidList(args.Field.Value);

        if (!value.Any() && args.Field.Required)
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
        return new ContentConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Multi-valued override: one searchable value per referenced id, mirroring <c>TreeFieldType</c>.
    /// </summary>
    public override IEnumerable<object> GetSearchableValues(FlexFieldValue field)
    {
        if (!field.Searchable || field.Value == null)
        {
            yield break;
        }

        foreach (var id in ReadGuidList(field.Value))
        {
            yield return id;
        }
    }

    /// <summary>
    /// <see cref="ReadStringList"/> already resolves every shape a value can arrive in (fresh
    /// <see cref="List{Guid}"/>, a single scalar, a <see cref="System.Text.Json.JsonElement"/> after a
    /// round trip) down to strings - reusing it here is simpler than re-deriving the same shape handling
    /// for <see cref="Guid"/>. Malformed entries are dropped rather than thrown on, the same lenient
    /// spirit the base helper itself is written in.
    /// </summary>
    private static List<Guid> ReadGuidList(object? value)
    {
        return ReadStringList(value)
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
