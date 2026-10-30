using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Templates;

[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/templates")]
public class SiteTemplatePublicController : SitePublicController, ISiteTemplatePublicAppService
{
    protected ISiteTemplatePublicAppService SiteTemplatePublicAppService { get; }

    public SiteTemplatePublicController(ISiteTemplatePublicAppService siteTemplatePublicAppService)
    {
        SiteTemplatePublicAppService = siteTemplatePublicAppService;
    }

    /// <summary>The name is a query parameter, not a route segment - it may contain slashes (<c>Blog/Article</c>).</summary>
    [HttpGet]
    [Route("render-set")]
    public virtual Task<SiteTemplateRenderSetDto> GetRenderSetAsync([FromQuery] string name)
    {
        return SiteTemplatePublicAppService.GetRenderSetAsync(name);
    }
}
