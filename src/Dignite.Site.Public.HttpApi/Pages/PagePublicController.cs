using System;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Pages;

[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/pages")]
public class PagePublicController : SitePublicController, IPagePublicAppService
{
    protected IPagePublicAppService PagePublicAppService { get; }

    public PagePublicController(IPagePublicAppService pagePublicAppService)
    {
        PagePublicAppService = pagePublicAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<PageDto> GetAsync(Guid id)
    {
        return PagePublicAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("by-route")]
    public virtual Task<PageDto> GetByRouteAsync([FromQuery] string route)
    {
        return PagePublicAppService.GetByRouteAsync(route);
    }

    /// <summary>The name is a query parameter, not a route segment - see <c>PageAdminController</c>'s own remarks.</summary>
    [HttpGet]
    [Route("by-name")]
    public virtual Task<PageDto?> FindByNameAsync(string name)
    {
        return PagePublicAppService.FindByNameAsync(name);
    }

    [HttpGet]
    public virtual Task<ListResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        return PagePublicAppService.GetListAsync(input);
    }
}
