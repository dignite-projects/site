using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dignite.Abp.FlexFields;
using Dignite.Site.Localization;

namespace Dignite.Site.Seo;

/// <summary>
/// The platform's SEO field type: one field whose value bundles meta title, meta description, a social
/// share image and the <c>noindex</c> flag (总体设计 §5.3, GitHub issue #14) - the 7th field type
/// alongside the six FlexFields built-ins, self-registered through DI exactly as §2.3 describes for
/// extension types (<c>ITransientDependency</c> via <see cref="FieldTypeBase"/>; <c>FieldTypeResolver</c>
/// collects every registered <see cref="IFieldType"/> automatically, no explicit wiring needed).
/// <para>
/// <b>Not indexable, deliberately</b> - same shape and same reasoning as FlexFields' own
/// <c>FileExplorerFieldType</c>, whose value is likewise a composite object rather than a bare scalar or
/// list of scalars. <c>noindex</c> does not need SQL-level querying: the sitemap generator that will
/// consume it (#15) already has to enumerate every published content to emit its URLs, so checking a
/// boolean while already iterating costs nothing - unlike an arbitrary filtered search, which is the case
/// the derived index exists for.
/// </para>
/// <para>
/// <b>Not og-image's final form.</b> The image is a plain URL string because no media/file field type
/// exists in this solution yet; adopting one is a separate change, not something to fold in here.
/// </para>
/// </summary>
public class SeoFieldType : FieldTypeBase
{
    public SeoFieldType()
    {
        LocalizationResource = typeof(SiteResource);
    }

    public override string Name => SeoFieldNames.FieldTypeName;

    public override string DisplayName => L["FieldType:Seo"];

    /// <summary>
    /// Null: the value is a composite object, not a single scalar or list of scalars - there is no typed
    /// index column to project it into. See this type's remarks for why <c>noindex</c> does not need one.
    /// </summary>
    public override FlexFieldValueType? IndexValueType => null;

    /// <summary>
    /// Structural only: a value, if present, has to be readable as <see cref="SeoFieldValue"/>. Nothing
    /// inside the bundle is required - SEO fields are supplementary, and a content type that pulls this
    /// field in still falls back to platform defaults for whatever is left blank (总体设计 §5.3) - and the
    /// character-limit guidance in <see cref="SeoConfiguration"/> is advisory, not enforced: a search
    /// engine truncates an over-length title rather than rejecting it, so treating it as a hard validation
    /// error would be over-strict for what is genuinely just a suggested threshold.
    /// </summary>
    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        if (TryRead(args.Field.Value, out _))
        {
            return Array.Empty<ValidationResult>();
        }

        return new[]
        {
            new ValidationResult(
                L["Validate:InvalidSeoValue", args.Field.DisplayName],
                new[] { args.Field.Name })
        };
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new SeoConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Storage-shape-agnostic read, in the spirit of <c>FieldTypeBase.ReadStringList</c> and
    /// <c>FileExplorerFieldType.HasAnyValue</c>: a fresh in-memory value is a live <see cref="SeoFieldValue"/>,
    /// one that has round-tripped through JSON storage is a <see cref="JsonElement"/>. Anything else -
    /// a bare string, a number, a list - is not a shape this field type ever produces, so it fails rather
    /// than being coerced into something misleading.
    /// </summary>
    private static bool TryRead(object? value, out SeoFieldValue? seoValue)
    {
        switch (value)
        {
            case null:
                seoValue = null;
                return true;
            case SeoFieldValue typed:
                seoValue = typed;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }:
                seoValue = null;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Object } element:
                seoValue = element.Deserialize<SeoFieldValue>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return true;
            default:
                seoValue = null;
                return false;
        }
    }
}
