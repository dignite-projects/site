using System;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.ContentTypes;

[RemoteService(Name = PublicRemoteServiceConsts.RemoteServiceName)]
[Area(PublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/content-types")]
public class ContentTypePublicController : PublicController, IContentTypePublicAppService
{
    protected IContentTypePublicAppService ContentTypePublicAppService { get; }

    public ContentTypePublicController(IContentTypePublicAppService contentTypePublicAppService)
    {
        ContentTypePublicAppService = contentTypePublicAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ContentTypeDto> GetAsync(Guid id)
    {
        return ContentTypePublicAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("by-page/{pageId}")]
    public virtual Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId)
    {
        return ContentTypePublicAppService.GetListByPageAsync(pageId);
    }
}
