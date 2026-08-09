using System.Collections.Generic;
using System.Linq;
using Dignite.Site.ContentTypes;

namespace Dignite.Site.Common;

/// <summary>
/// Converts between <c>ContentTypeField</c> (the domain value object) and <c>ContentTypeFieldDto</c>.
/// Hand-written rather than left to Mapperly: the entity side has no public constructor Mapperly could
/// target without also touching the required-property inference for a plain positional constructor call,
/// and getting that silently wrong (dropping <c>ShowInList</c>, say) is worse than six explicit lines.
/// Shared for the same reason as <see cref="FlexFieldValueDictionaryExtensions"/> - a plain static method,
/// not a per-module Mapperly registration.
/// </summary>
public static class ContentTypeFieldDtoExtensions
{
    public static ContentTypeFieldDto ToDto(this ContentTypeField source)
    {
        return new ContentTypeFieldDto
        {
            FieldId = source.FieldId,
            Required = source.Required,
            Searchable = source.Searchable,
            ShowInList = source.ShowInList,
            DisplayName = source.DisplayName,
            Order = source.Order
        };
    }

    public static List<ContentTypeFieldDto> ToDtoList(this IEnumerable<ContentTypeField> source)
    {
        return source.Select(ToDto).ToList();
    }

    public static ContentTypeField ToEntity(this ContentTypeFieldDto source)
    {
        return new ContentTypeField(
            source.FieldId, source.Required, source.Searchable, source.ShowInList, source.DisplayName, source.Order);
    }

    public static List<ContentTypeField> ToEntityList(this IEnumerable<ContentTypeFieldDto> source)
    {
        return source.Select(ToEntity).ToList();
    }
}
