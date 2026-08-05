using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Fields;

[RemoteService(Name = PublicRemoteServiceConsts.RemoteServiceName)]
[Area(PublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/fields")]
public class FieldPublicController : PublicController, IFieldPublicAppService
{
    protected IFieldPublicAppService FieldPublicAppService { get; }

    public FieldPublicController(IFieldPublicAppService fieldPublicAppService)
    {
        FieldPublicAppService = fieldPublicAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<FieldDto> GetAsync(Guid id)
    {
        return FieldPublicAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<ListResultDto<FieldDto>> GetListAsync([FromQuery] IEnumerable<Guid> ids)
    {
        return FieldPublicAppService.GetListAsync(ids);
    }
}
