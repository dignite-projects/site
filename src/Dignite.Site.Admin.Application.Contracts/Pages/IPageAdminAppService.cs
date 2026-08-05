using System;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Pages;

public interface IPageAdminAppService : IApplicationService
{
    Task<PageDto> GetAsync(Guid id);

    Task<PagedResultDto<PageDto>> GetListAsync(GetPageListInput input);

    Task<PageDto> CreateAsync(CreatePageDto input);

    Task<PageDto> UpdateAsync(Guid id, UpdatePageDto input);

    /// <summary>
    /// Deleting a page cascades to its content types and contents at the database level - there is no
    /// separate guard to run here (总体设计 §2.5).
    /// </summary>
    Task DeleteAsync(Guid id);
}
