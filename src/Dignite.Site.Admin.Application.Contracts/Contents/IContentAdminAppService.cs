using System;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Contents;

public interface IContentAdminAppService : IApplicationService
{
    Task<ContentDto> GetAsync(Guid id);

    Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input);

    Task<ContentDto> CreateAsync(CreateContentDto input);

    Task<ContentDto> UpdateAsync(Guid id, UpdateContentDto input);

    Task DeleteAsync(Guid id);
}
