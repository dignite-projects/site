using System;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Pages;

public interface IPageAdminAppService : IApplicationService
{
    Task<PageDto> GetAsync(Guid id);

    /// <summary>
    /// Finds a page by its tenant-unique <see cref="PageDto.Name"/>, or null. The lookup the MCP tool
    /// surface addresses pages by, so a client never has to carry a Guid (总体设计 §6.2.4).
    /// </summary>
    Task<PageDto?> FindByNameAsync(string name);

    Task<PagedResultDto<PageDto>> GetListAsync(GetPageListInput input);

    Task<PageDto> CreateAsync(CreatePageDto input);

    Task<PageDto> UpdateAsync(Guid id, UpdatePageDto input);

    /// <summary>
    /// Deleting a page cascades to its content types and contents. The cascade runs explicitly in
    /// <c>PageManager.DeleteAsync</c>, not at the database level: these entities are soft-deleted, so a
    /// delete is an UPDATE and a declared <c>ON DELETE CASCADE</c> never fires. There is no separate guard
    /// to run here - the cascade is unconditional (总体设计 §2.5).
    /// </summary>
    Task DeleteAsync(Guid id);
}
