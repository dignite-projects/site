using System;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.ContentTypes;

public interface IContentTypePublicAppService : IApplicationService
{
    Task<ContentTypeDto> GetAsync(Guid id);

    Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId);
}
