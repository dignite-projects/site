using System;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Pages;

[RemoteService(Name = PublicRemoteServiceConsts.RemoteServiceName)]
[Area(PublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/pages")]
public class PagePublicController : PublicController, IPagePublicAppService
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

    [HttpGet]
    public virtual Task<PagedResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        return PagePublicAppService.GetListAsync(input);
    }
}
