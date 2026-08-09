using System;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.ContentTypes;

public interface IContentTypeAdminAppService : IApplicationService
{
    Task<ContentTypeDto> GetAsync(Guid id);

    /// <summary>
    /// Finds a content type by its name within a page, or null. A content type's name is unique per page
    /// rather than per tenant, which is why this takes both (总体设计 §2.5, §6.2.4).
    /// </summary>
    Task<ContentTypeDto?> FindByNameAsync(Guid pageId, string name);

    Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId);

    /// <summary>
    /// Every content type across every page, unfiltered. The Contents list needs this to resolve a
    /// content's <c>ContentTypeId</c> to a display name regardless of which page (if any) is selected in
    /// its filter - <see cref="GetListByPageAsync"/> alone cannot answer that for a row whose page is not
    /// the one currently filtered on.
    /// </summary>
    Task<ListResultDto<ContentTypeDto>> GetListAsync();

    Task<ContentTypeDto> CreateAsync(CreateContentTypeDto input);

    Task<ContentTypeDto> UpdateAsync(Guid id, UpdateContentTypeDto input);

    /// <summary>
    /// Rejected while any content still uses this type - deleting it out from under existing content
    /// would silently break a page's rendering. This check is enforced in application code, not by the
    /// database: the FK from Content to ContentType is declared <c>Restrict</c>, but ContentType is
    /// soft-deleted, so a "delete" is an UPDATE and a declared FK behavior - which fires only on an
    /// actual DELETE - never runs. Move or delete the contents first.
    /// </summary>
    Task DeleteAsync(Guid id);
}
