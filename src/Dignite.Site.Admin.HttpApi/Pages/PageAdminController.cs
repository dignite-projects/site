using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Pages;

[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Pages.Default)]
[Route("api/site-admin/pages")]
public class PageAdminController : SiteAdminController, IPageAdminAppService
{
    protected IPageAdminAppService PageAdminAppService { get; }

    public PageAdminController(IPageAdminAppService pageAdminAppService)
    {
        PageAdminAppService = pageAdminAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<PageDto> GetAsync(Guid id)
    {
        return PageAdminAppService.GetAsync(id);
    }

    /// <summary>
    /// The name is a query parameter, not a route segment. Nothing constrains a page's name to
    /// URL-path-safe characters - <c>Page.SetName</c> checks only blankness and length, and the MCP
    /// <c>create_page</c> tool hands the choice to a model - so a name containing a slash or a dot would
    /// be unreachable as a segment (the path is decoded before routing, so it presents as extra segments
    /// and matches nothing), while remaining perfectly addressable everywhere else.
    /// </summary>
    [HttpGet]
    [Route("by-name")]
    public virtual Task<PageDto?> FindByNameAsync(string name)
    {
        return PageAdminAppService.FindByNameAsync(name);
    }

    [HttpGet]
    public virtual Task<ListResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        return PageAdminAppService.GetListAsync(input);
    }

    [HttpPost]
    [Authorize(SiteAdminPermissions.Pages.Create)]
    public virtual Task<PageDto> CreateAsync(CreatePageDto input)
    {
        return PageAdminAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.Pages.Update)]
    public virtual Task<PageDto> UpdateAsync(Guid id, UpdatePageDto input)
    {
        return PageAdminAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(SiteAdminPermissions.Pages.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return PageAdminAppService.DeleteAsync(id);
    }
}
