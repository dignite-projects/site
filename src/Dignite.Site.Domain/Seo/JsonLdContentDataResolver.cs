using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Seo;

/// <summary>
/// Reads a content's JSON-LD property values off whatever fields its content type mapped through
/// <see cref="ContentTypeField.SchemaProperty"/> (总体设计 §5.4, GitHub issue #20).
/// </summary>
public class JsonLdContentDataResolver : DomainService
{
    /// <summary>
    /// The tenant-mappable properties commonly expected of each schema.org type - surfaced as
    /// <see cref="JsonLdContentData.ExpectedProperties"/> for a local structural check, not a contract this
    /// resolver enforces.
    /// <para>
    /// Deliberately excludes properties this resolver always supplies structurally rather than through a
    /// field mapping - <c>datePublished</c>/<c>dateModified</c> come from <see cref="Content.PublishTime"/>
    /// and <see cref="ContentModificationTime"/>, never from <see cref="JsonLdContentData.PropertyValues"/>,
    /// so listing them here would flag every content as "missing" something that is never actually absent.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<SchemaOrgType, IReadOnlyList<string>> ExpectedProperties =
        new Dictionary<SchemaOrgType, IReadOnlyList<string>>
        {
            [SchemaOrgType.Article] = new[] { "headline", "image" },
            [SchemaOrgType.NewsArticle] = new[] { "headline", "image" },
            [SchemaOrgType.Product] = new[] { "name", "image" }
        };

    protected IFieldRepository FieldRepository { get; }

    public JsonLdContentDataResolver(IFieldRepository fieldRepository)
    {
        FieldRepository = fieldRepository;
    }

    /// <summary>Null when <paramref name="contentType"/> has not opted into a schema.org type.</summary>
    public virtual async Task<JsonLdContentData?> ResolveAsync(
        ContentType contentType, Content content, CancellationToken cancellationToken = default)
    {
        if (contentType.SchemaType == SchemaOrgType.None)
        {
            return null;
        }

        var mappedFields = contentType.Fields.Where(f => !string.IsNullOrEmpty(f.SchemaProperty)).ToList();

        var propertyValues = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (mappedFields.Count > 0)
        {
            // The whole library, not a lookup by id: the tenant's field library is small and this
            // resolves once per rendered route, the same trade-off ContentSummaryResolver makes for its
            // own per-page lookup.
            var fieldsById = (await FieldRepository.GetListAsync(cancellationToken: cancellationToken))
                .ToDictionary(f => f.Id);

            foreach (var mapped in mappedFields)
            {
                if (!fieldsById.TryGetValue(mapped.FieldId, out var field))
                {
                    continue;
                }

                var value = ReadValue(content, field.Name);

                // A blank string counts as unset, the same as null - otherwise a mapped-but-empty field
                // (title -> "headline", saved blank) reads as "present" here while BuildContentNode's own
                // ApplyString skips it as blank, so the missing-headline warning this exists to produce
                // never fires for exactly the content that needs it.
                if (value != null && !(value is string text && string.IsNullOrWhiteSpace(text)))
                {
                    propertyValues[mapped.SchemaProperty!] = value;
                }
            }
        }

        var expected = ExpectedProperties.TryGetValue(contentType.SchemaType, out var names)
            ? names
            : Array.Empty<string>();

        return new JsonLdContentData(
            contentType.SchemaType,
            propertyValues,
            content.PublishTime,
            ContentModificationTime.Of(content),
            expected);
    }

    /// <summary>
    /// Reads one field's raw value in whatever shape it happens to be stored - a live in-memory value
    /// before a save, or a <see cref="JsonElement"/> after a database round trip, the same duality
    /// <c>SeoFieldType</c> and <c>ContentSummaryResolver</c> handle. Fails open per field: an unreadable or
    /// non-scalar shape is skipped and logged rather than taking the rest of the node down with it.
    /// <para>
    /// Both shapes are rejected identically for a non-scalar value - a live in-memory <c>List&lt;string&gt;</c>
    /// (from a multi-value Select field, say) is not accepted just because it has not been serialized yet;
    /// otherwise the same stored data would resolve differently depending only on whether the entity
    /// happened to still be tracked, which is exactly the kind of answer a caller cannot rely on.
    /// </para>
    /// </summary>
    protected virtual object? ReadValue(Content content, string fieldName)
    {
        try
        {
            var raw = content.GetField(fieldName);

            return raw switch
            {
                null => null,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                JsonElement { ValueKind: JsonValueKind.Number } element =>
                    element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
                JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False } element => element.GetBoolean(),
                JsonElement => null, // null/undefined, or an object/array - not a meaningful JSON-LD scalar
                // Normalized to the same round-trip ("O") form JSON storage would have produced, so a date
                // read before a save and the same date read after one are the same value here.
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                Guid guid => guid.ToString(),
                char character => character.ToString(),
                Enum enumeration => enumeration.ToString(),
                string or bool
                    or byte or sbyte or short or ushort or int or uint or long or ulong
                    or float or double or decimal => raw,
                // Any other in-memory CLR shape (a list, a dictionary, a composite field value) is not a
                // scalar either - reject it the same way the JsonElement branch above does.
                _ => null
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Content {ContentId}'s '{FieldName}' value could not be read for JSON-LD; omitting it.",
                content.Id, fieldName);
            return null;
        }
    }
}
