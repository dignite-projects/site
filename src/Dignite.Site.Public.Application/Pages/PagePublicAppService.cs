using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace Dignite.Site.Public.Pages;

public class PagePublicAppService : PublicAppService, IPagePublicAppService
{
    protected IPageRepository PageRepository { get; }

    public PagePublicAppService(IPageRepository pageRepository)
    {
        PageRepository = pageRepository;
    }

    public virtual async Task<PageDto> GetAsync(Guid id)
    {
        var page = await PageRepository.GetAsync(id);
        EnsureActive(page);
        return ObjectMapper.Map<Page, PageDto>(page);
    }

    public virtual async Task<PageDto> GetByRouteAsync(string route)
    {
        var page = await PageRepository.FindByRouteAsync(route);

        if (page == null || !page.IsActive)
        {
            throw new EntityNotFoundException(typeof(Page));
        }

        return ObjectMapper.Map<Page, PageDto>(page);
    }

    public virtual async Task<PagedResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        var totalCount = await PageRepository.GetCountAsync(isActive: true, filter: input.Filter);

        var pages = await PageRepository.GetListAsync(
            isActive: true, filter: input.Filter, maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount, sorting: input.Sorting);

        var items = pages.Select(page => ObjectMapper.Map<Page, PageDto>(page)).ToList();

        return new PagedResultDto<PageDto>(totalCount, items);
    }

    /// <summary>
    /// An inactive page is not routable and must not be discoverable by direct id either - the caller
    /// should see the same "not found" it would get for a random guid, not learn the page exists at all.
    /// </summary>
    protected virtual void EnsureActive(Page page)
    {
        if (!page.IsActive)
        {
            throw new EntityNotFoundException(typeof(Page), page.Id);
        }
    }
}
