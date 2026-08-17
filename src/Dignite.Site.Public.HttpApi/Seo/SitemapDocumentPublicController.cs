using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Public.Seo;

[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route("api/site-public/sitemap")]
public class SitemapDocumentPublicController : SitePublicController, ISitemapDocumentAppService
{
    protected ISitemapDocumentAppService SitemapDocumentAppService { get; }

    public SitemapDocumentPublicController(ISitemapDocumentAppService sitemapDocumentAppService)
    {
        SitemapDocumentAppService = sitemapDocumentAppService;
    }

    [HttpGet]
    public virtual Task<SiteDocument> GetIndexAsync()
    {
        return SitemapDocumentAppService.GetIndexAsync();
    }

    [HttpGet]
    [Route("shard")]
    public virtual Task<SiteDocument?> GetShardAsync([FromQuery] int shardNumber)
    {
        return SitemapDocumentAppService.GetShardAsync(shardNumber);
    }
}
