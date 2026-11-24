using System.Linq;
using Dignite.Site.ContentTypes;
using Dignite.Site.Pages;
using Volo.Abp.ObjectMapping;

namespace Dignite.Site.Common;

public static class PageDtoExtensions
{
    /// <summary>
    /// The page with its content types, for a page read with its details (<c>includeDetails: true</c>), whose
    /// <see cref="Page.ContentTypes"/> are loaded. A plain <c>ObjectMapper.Map</c> leaves
    /// <see cref="PageDto.ContentTypes"/> null, which tells a reader they were not loaded rather than that
    /// there are none.
    /// </summary>
    public static PageDto ToDtoWithContentTypes(this Page page, IObjectMapper objectMapper)
    {
        var dto = objectMapper.Map<Page, PageDto>(page);
        dto.ContentTypes = page.ContentTypes.Select(objectMapper.Map<ContentType, ContentTypeDto>).ToList();
        return dto;
    }
}
