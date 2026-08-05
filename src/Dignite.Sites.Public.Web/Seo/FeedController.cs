using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Caching;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// A page's feed, at <c>/blog/feed.xml</c>, <c>/blog/atom.xml</c> or <c>/blog/feed.json</c> - and the
/// language-prefixed forms of the same, e.g. <c>/zh-Hans/about/feed.xml</c>
/// (总体设计 §5.9, GitHub issue #31).
/// </summary>
public class FeedController : SiteDocumentController
{
    /// <summary>
    /// Ten minutes. A feed is the "what just changed" document, so it is the one of these three that
    /// benefits from being nearly live.
    /// </summary>
    public const int MaxAgeSeconds = 600;

    protected IFeedDocumentAppService FeedDocumentAppService { get; }

    public FeedController(
        IFeedDocumentAppService feedDocumentAppService,
        IDistributedCache<SiteDocumentCacheItem, string> cache)
        : base(cache)
    {
        FeedDocumentAppService = feedDocumentAppService;
    }

    /// <summary>
    /// <para>
    /// A catch-all narrowed by <see cref="FeedPathRouteConstraint"/> - see there for why the conventional
    /// <c>{page}/feed.xml</c> shape cannot be written as an ordinary template. Every request that is not a
    /// feed address falls through to the routes that should handle it (and, once GitHub issue #21 lands,
    /// to the MVC catch-all beneath them).
    /// </para>
    /// <para>
    /// Known and accepted: a content whose slug is literally <c>feed.xml</c> is shadowed by its page's
    /// feed, because a compiled route outranks the runtime routing table. Every CMS that serves feeds at
    /// this shape makes the same trade.
    /// </para>
    /// </summary>
    [AcceptVerbs("GET", "HEAD", Route = "/{**feedPath:" + FeedPathRouteConstraint.Name + "}")]
    public virtual Task<IActionResult> GetAsync(string feedPath)
    {
        return ServeAsync(
            "feed:" + feedPath,
            () => FeedDocumentAppService.ResolveAsync(feedPath),
            MaxAgeSeconds,
            compressible: true);
    }
}
