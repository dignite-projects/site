using System;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Contents;

public interface IContentAdminAppService : IApplicationService
{
    Task<ContentDto> GetAsync(Guid id);

    /// <summary>
    /// Finds a content by the triple that uniquely identifies it - page, language and slug - or null.
    /// This is the natural address for a caller working in names (总体设计 §6.2.4): it is the same
    /// <c>(PageId, CultureName, Slug)</c> key the unique constraint and route resolution use, so an
    /// empty <paramref name="slug"/> legitimately addresses the page's single content (§2.4).
    /// <para>
    /// <paramref name="cultureName"/> is normalized before the lookup, because the stored value is
    /// normalized (see <c>ContentManager.CreateAsync</c>): querying a keyed lookup with the caller's raw
    /// <c>zh-cn</c> against a stored <c>zh-Hans</c> would report "not found" for a content that exists.
    /// </para>
    /// </summary>
    Task<ContentDto?> FindBySlugAsync(Guid pageId, string cultureName, string slug);

    Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input);

    Task<ContentDto> CreateAsync(CreateContentDto input);

    Task<ContentDto> UpdateAsync(Guid id, UpdateContentDto input);

    Task DeleteAsync(Guid id);
}
