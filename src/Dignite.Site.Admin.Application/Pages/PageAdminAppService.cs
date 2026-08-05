using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Pages;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Pages;

[Authorize(AdminPermissions.Pages.Default)]
public class PageAdminAppService : AdminAppService, IPageAdminAppService
{
    protected IPageRepository PageRepository { get; }

    protected PageManager PageManager { get; }

    public PageAdminAppService(IPageRepository pageRepository, PageManager pageManager)
    {
        PageRepository = pageRepository;
        PageManager = pageManager;
    }

    public virtual async Task<PageDto> GetAsync(Guid id)
    {
        var page = await PageRepository.GetAsync(id);
        return ObjectMapper.Map<Page, PageDto>(page);
    }

    public virtual async Task<PagedResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        var totalCount = await PageRepository.GetCountAsync(isActive: input.IsActive, filter: input.Filter);

        var pages = await PageRepository.GetListAsync(
            isActive: input.IsActive, filter: input.Filter, maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount, sorting: input.Sorting);

        var items = pages.Select(page => ObjectMapper.Map<Page, PageDto>(page)).ToList();

        return new PagedResultDto<PageDto>(totalCount, items);
    }

    [Authorize(AdminPermissions.Pages.Create)]
    public virtual async Task<PageDto> CreateAsync(CreatePageDto input)
    {
        var page = await PageManager.CreateAsync(
            input.Name, input.DisplayName, input.Route, input.ContentPathPattern, input.Template,
            input.IsHomePage, input.Order, input.IsActive);

        return ObjectMapper.Map<Page, PageDto>(page);
    }

    [Authorize(AdminPermissions.Pages.Update)]
    public virtual async Task<PageDto> UpdateAsync(Guid id, UpdatePageDto input)
    {
        var page = await PageRepository.GetAsync(id);

        page = await PageManager.UpdateAsync(
            page, input.Name, input.DisplayName, input.Route, input.ContentPathPattern, input.Template,
            input.IsHomePage, input.Order, input.IsActive);

        return ObjectMapper.Map<Page, PageDto>(page);
    }

    [Authorize(AdminPermissions.Pages.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Cascades to this page's content types and contents at the database level (总体设计 §2.5) - no
        // guard to run here, unlike ContentTypeAdminAppService.DeleteAsync.
        await PageRepository.DeleteAsync(id);
    }
}
