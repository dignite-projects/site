using System.Threading.Tasks;
using Dignite.Site.Seo;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// A page's feed, per language and per format (总体设计 §5.9, GitHub issue #31).
/// </summary>
public interface IFeedDocumentAppService : IApplicationService
{
    /// <param name="pageRoute">The page's route, e.g. <c>/blog</c>. Normalized before it is looked up.</param>
    /// <param name="cultureName">Which language's contents the feed carries.</param>
    /// <returns><see langword="null"/> when no active page has that route, or the culture is not a real one.</returns>
    Task<SiteDocument?> GetAsync(string pageRoute, string cultureName, SiteFeedFormat format);

    /// <summary>
    /// Takes apart a feed request path - <c>zh-Hans/about/feed.xml</c>, <c>blog/atom.xml</c> - into
    /// language, page route and format, then serves it.
    /// <para>
    /// Lives here rather than in the endpoint because the language prefix is stripped with the same
    /// <c>SiteUrlContext</c> that put it on: the two directions have to stay in one place, or the site
    /// starts advertising feed URLs it cannot resolve.
    /// </para>
    /// </summary>
    /// <returns><see langword="null"/> when the path is not a feed address this site serves.</returns>
    Task<SiteDocument?> ResolveAsync(string feedPath);
}
