using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Common;
using Dignite.Site.ContentTypes;
using Dignite.Site.Pages;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace Dignite.Site.Public.Pages;

public class PagePublicAppService : SitePublicAppService, IPagePublicAppService
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
        return page.ToDtoWithContentTypes(ObjectMapper);
    }

    public virtual async Task<PageDto> GetByRouteAsync(string route)
    {
        var page = await PageRepository.FindByPathAsync(route, includeDetails: true);

        if (page == null)
        {
            throw new EntityNotFoundException(typeof(Page));
        }

        return page.ToDtoWithContentTypes(ObjectMapper);
    }

    public virtual async Task<PageDto?> FindByNameAsync(string name)
    {
        var page = await PageRepository.FindByNameAsync(name, includeDetails: true);
        return page == null || !page.IsActive ? null : page.ToDtoWithContentTypes(ObjectMapper);
    }

    public virtual async Task<ListResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        var pages = await PageRepository.GetListAsync(isActive: true, filter: input.Filter);

        var items = pages.Select(page => ObjectMapper.Map<Page, PageDto>(page)).ToList();

        return new ListResultDto<PageDto>(items);
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
