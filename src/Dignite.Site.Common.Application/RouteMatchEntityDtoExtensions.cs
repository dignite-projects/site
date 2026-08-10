using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Volo.Abp.ObjectMapping;

namespace Dignite.Site.Common;

/// <summary>
/// Materializes a resolved route's <c>Content</c>/<c>ContentType</c> into their DTOs - the glue
/// <c>ObjectMapper.Map&lt;,&gt;</c> alone can't express (<see cref="ContentDto.FieldValues"/> and
/// <see cref="ContentTypeDto.Fields"/> are hand-converted, not Mapperly-mapped). Shared for the same
/// reason as <see cref="FlexFieldValueDictionaryExtensions"/> and <see cref="ContentTypeFieldDtoExtensions"/>
/// - every caller that already holds a resolved <c>RouteMatch</c> (in-process or out-of-process, Public
/// application service or a same-process renderer) needs the identical conversion, and a second
/// hand-written copy is exactly how two "canonical" builders of the same DTO quietly disagree.
/// </summary>
public static class RouteMatchEntityDtoExtensions
{
    public static ContentDto ToDto(this Content content, IObjectMapper objectMapper)
    {
        var dto = objectMapper.Map<Content, ContentDto>(content);
        dto.FieldValues = content.FlexFields.ToValueDictionary();
        return dto;
    }

    public static ContentTypeDto ToDto(this ContentType contentType, IObjectMapper objectMapper)
    {
        var dto = objectMapper.Map<ContentType, ContentTypeDto>(contentType);
        dto.Fields = contentType.Fields.ToDtoList();
        return dto;
    }
}
