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

    Task<ListResultDto<PageDto>> GetListAsync(GetPageListInput input);

    Task<PageDto> CreateAsync(CreatePageDto input);

    Task<PageDto> UpdateAsync(Guid id, UpdatePageDto input);

    /// <summary>
    /// Reparents and/or repositions a page - what a drag-and-drop in the Admin UI's page list calls. See
    /// <see cref="MovePageDto.Order"/> for why this is not just a thinner <see cref="UpdateAsync"/>.
    /// </summary>
    Task<PageDto> MoveAsync(Guid id, MovePageDto input);

    /// <summary>
    /// Deleting a page cascades to its content types and contents - that cascade runs explicitly in
    /// <c>PageManager.DeleteAsync</c>, not at the database level, since these entities are soft-deleted so
    /// a delete is an UPDATE and a declared <c>ON DELETE CASCADE</c> never fires (总体设计 §2.5). It does
    /// <b>not</b> cascade to child pages, though: a page with children is refused
    /// (<c>PageHasChildrenException</c>) rather than taking a whole subtree - each with its own content
    /// types and contents - down with it. Move or delete the children first.
    /// </summary>
    Task DeleteAsync(Guid id);
}
