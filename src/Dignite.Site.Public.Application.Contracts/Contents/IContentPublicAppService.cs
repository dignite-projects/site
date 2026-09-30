using System;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// A draft or not-yet-due scheduled content is invisible here regardless of id or slug. An archived one is
/// the exception: its own detail path and translations still answer, though it never appears in a list
/// (<see cref="Content.IsPubliclyAccessible"/>).
/// </summary>
public interface IContentPublicAppService : IReadOnlyAppService
    <ContentDto, Guid, GetContentListInput>
{
    /// <summary>Step 2 of route resolution (总体设计 §3.4).</summary>
    Task<ContentDto> GetBySlugAsync(Guid pageId, string cultureName, string slug);

    /// <summary>Every language version of one content, for hreflang and a language switcher (总体设计 §5.5).</summary>
    Task<ListResultDto<ContentDto>> GetTranslationsAsync(Guid pageId, Guid contentTypeId, string slug);

    /// <summary>
    /// The previous (older) and next (newer) content by <c>PublishTime</c>, for a detail page's
    /// previous/next links. <paramref name="id"/> itself answers wherever <c>GetAsync</c> would - an archived
    /// one included - but its neighbors are drawn only from what a list would show.
    /// </summary>
    Task<AdjacentContentsDto> GetAdjacentAsync(Guid id, GetAdjacentContentsInput input);
}
