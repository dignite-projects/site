using System;
using System.Threading.Tasks;
using Dignite.Sites.ContentTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Admin.ContentTypes;

public interface IContentTypeAdminAppService : IApplicationService
{
    Task<ContentTypeDto> GetAsync(Guid id);

    Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId);

    Task<ContentTypeDto> CreateAsync(CreateContentTypeDto input);

    Task<ContentTypeDto> UpdateAsync(Guid id, UpdateContentTypeDto input);

    /// <summary>
    /// Rejected while any content still uses this type - deleting it out from under existing content
    /// would either orphan rows (the FK is <c>Restrict</c>, so the database refuses it anyway) or, worse,
    /// silently break a page's rendering. Move or delete the contents first.
    /// </summary>
    Task DeleteAsync(Guid id);
}
