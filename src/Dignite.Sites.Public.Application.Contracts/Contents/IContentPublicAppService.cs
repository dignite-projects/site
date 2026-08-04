using System;
using System.Threading.Tasks;
using Dignite.Sites.Contents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Public.Contents;

/// <summary>Published content only - a draft or scheduled content is invisible here regardless of id or slug.</summary>
public interface IContentPublicAppService : IApplicationService
{
    Task<ContentDto> GetAsync(Guid id);

    /// <summary>Step 2 of route resolution (总体设计 §3.4).</summary>
    Task<ContentDto> GetBySlugAsync(Guid pageId, string cultureName, string slug);

    Task<PagedResultDto<ContentDto>> GetListAsync(GetPublicContentListInput input);

    /// <summary>Every language version of one content, for hreflang and a language switcher (总体设计 §5.5).</summary>
    Task<ListResultDto<ContentDto>> GetTranslationsAsync(Guid pageId, Guid contentTypeId, string slug);
}
