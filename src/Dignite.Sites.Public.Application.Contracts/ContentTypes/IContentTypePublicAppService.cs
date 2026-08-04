using System;
using System.Threading.Tasks;
using Dignite.Sites.ContentTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Public.ContentTypes;

public interface IContentTypePublicAppService : IApplicationService
{
    Task<ContentTypeDto> GetAsync(Guid id);

    Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId);
}
