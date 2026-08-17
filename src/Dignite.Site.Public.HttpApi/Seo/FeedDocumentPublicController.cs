using System.Threading.Tasks;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Seo;

[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/feed")]
public class FeedDocumentPublicController : SitePublicController, IFeedDocumentAppService
{
    protected IFeedDocumentAppService FeedDocumentAppService { get; }

    public FeedDocumentPublicController(IFeedDocumentAppService feedDocumentAppService)
    {
        FeedDocumentAppService = feedDocumentAppService;
    }

    [HttpGet]
    public virtual Task<SiteDocument?> GetAsync(
        [FromQuery] string pagePath, [FromQuery] string cultureName, [FromQuery] SiteFeedFormat format)
    {
        return FeedDocumentAppService.GetAsync(pagePath, cultureName, format);
    }

    [HttpGet]
    [Route("resolve")]
    public virtual Task<SiteDocument?> ResolveAsync([FromQuery] string feedPath)
    {
        return FeedDocumentAppService.ResolveAsync(feedPath);
    }
}
