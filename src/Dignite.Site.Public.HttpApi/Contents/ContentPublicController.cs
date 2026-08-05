using System;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Contents;

[RemoteService(Name = PublicRemoteServiceConsts.RemoteServiceName)]
[Area(PublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/contents")]
public class ContentPublicController : PublicController, IContentPublicAppService
{
    protected IContentPublicAppService ContentPublicAppService { get; }

    public ContentPublicController(IContentPublicAppService contentPublicAppService)
    {
        ContentPublicAppService = contentPublicAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ContentDto> GetAsync(Guid id)
    {
        return ContentPublicAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("by-slug/{pageId}")]
    public virtual Task<ContentDto> GetBySlugAsync(
        Guid pageId, [FromQuery] string cultureName, [FromQuery] string slug)
    {
        return ContentPublicAppService.GetBySlugAsync(pageId, cultureName, slug);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ContentDto>> GetListAsync(GetPublicContentListInput input)
    {
        return ContentPublicAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("translations")]
    public virtual Task<ListResultDto<ContentDto>> GetTranslationsAsync(
        [FromQuery] Guid pageId, [FromQuery] Guid contentTypeId, [FromQuery] string slug)
    {
        return ContentPublicAppService.GetTranslationsAsync(pageId, contentTypeId, slug);
    }
}
