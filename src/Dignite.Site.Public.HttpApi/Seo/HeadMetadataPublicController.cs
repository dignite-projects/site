using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Seo;

[RemoteService(Name = PublicRemoteServiceConsts.RemoteServiceName)]
[Area(PublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/head-metadata")]
public class HeadMetadataPublicController : PublicController, IHeadMetadataPublicAppService
{
    protected IHeadMetadataPublicAppService HeadMetadataPublicAppService { get; }

    public HeadMetadataPublicController(IHeadMetadataPublicAppService headMetadataPublicAppService)
    {
        HeadMetadataPublicAppService = headMetadataPublicAppService;
    }

    [HttpGet]
    public virtual Task<HeadMetadataDto?> ResolveAsync([FromQuery] ResolveHeadMetadataInput input)
    {
        return HeadMetadataPublicAppService.ResolveAsync(input);
    }
}
