using System;
using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Public.Application.Contracts.Routing;
using Dignite.Site.Seo;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// Builds the <see cref="FlexFieldValue"/> list a view renders via <c>&lt;flex-field-view&gt;</c>, from
/// DTOs already fetched through the Public application services only - never by calling Domain-layer
/// FlexFields providers directly, which would bypass <c>IContentPublicAppService</c>'s published-only
/// filtering.
/// </summary>
public static class SiteRenderFieldMapper
{
    public static IReadOnlyList<FlexFieldValue> Build(
        ContentDto content,
        ContentTypeDto contentType,
        IReadOnlyDictionary<Guid, FieldDto> fieldsById)
    {
        var result = new List<FlexFieldValue>(contentType.Fields.Count);

        foreach (var usage in contentType.Fields) // already content-type order, per ContentTypeDto.Fields
        {
            if (!fieldsById.TryGetValue(usage.FieldId, out var field))
            {
                continue; // definition deleted since the content type referenced it - skip, don't error
            }

            if (field.Name == SeoFieldNames.FieldName)
            {
                // No FlexFields/Seo.cshtml partial ships anywhere - SEO metadata is consumed separately via
                // IHeadMetadataPublicAppService, never printed inline in page body. Keyed on Name (the one
                // property FieldManager actually protects from edits, per SeoFieldNames' own doc comment) -
                // FieldTypeName is freely editable, so keying on it would stop skipping the moment an admin
                // changed the seeded field's type.
                continue;
            }

            var data = new FlexFieldData
            {
                Id = field.Id,
                Name = field.Name,
                DisplayName = usage.DisplayName ?? field.DisplayName,
                Description = field.Description,
                FieldTypeName = field.FieldTypeName,
                Configuration = field.Configuration.ToFieldConfiguration()
            };

            content.FieldValues.TryGetValue(field.Name, out var value);
            result.Add(new FlexFieldValue(data, usage.Required, usage.Searchable, value));
        }

        return result;
    }
}
