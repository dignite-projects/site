using System.Threading;
using System.Threading.Tasks;
using Dignite.Sites.Contents;
using Dignite.Sites.Pages;
using Volo.Abp.Domain.Services;

namespace Dignite.Sites.Seo;

/// <summary>
/// The one place an absolute site URL is composed (总体设计 §4.2, §5.2, §5.5). Sitemap (#15), robots (#18)
/// and feeds (#31) all go through it, and so should canonical (#16) and hreflang (#17) - a second URL
/// builder anywhere means two answers to "what is this page's address".
/// <para>
/// Split into an async <see cref="CreateContextAsync"/> and synchronous composition on the returned
/// <see cref="SiteUrlContext"/>, the same shape as <c>NoIndexRecognizer.FindFieldAsync</c> +
/// <c>IsNoIndex</c>: a sitemap run resolves the settings once and then composes tens of thousands of URLs
/// with no further I/O.
/// </para>
/// </summary>
public class SiteUrlBuilder : DomainService
{
    protected ISiteBaseUrlResolver BaseUrlResolver { get; }

    protected SiteLanguageProvider LanguageProvider { get; }

    public SiteUrlBuilder(ISiteBaseUrlResolver baseUrlResolver, SiteLanguageProvider languageProvider)
    {
        BaseUrlResolver = baseUrlResolver;
        LanguageProvider = languageProvider;
    }

    /// <exception cref="PrimaryDomainNotConfiguredException">
    /// No base URL is configured and none could be inferred.
    /// </exception>
    public virtual async Task<SiteUrlContext> CreateContextAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = await BaseUrlResolver.GetBaseUrlAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new PrimaryDomainNotConfiguredException();
        }

        var languages = await LanguageProvider.GetEnabledLanguagesAsync(cancellationToken);

        return new SiteUrlContext(baseUrl!, languages[0], languages);
    }

    /// <summary>The absolute URL of a page's own route - its list view, or its single content.</summary>
    public virtual string BuildPageUrl(SiteUrlContext context, Page page, string cultureName)
    {
        return context.BuildAbsolute(page.Route, cultureName);
    }

    /// <summary>
    /// The absolute URL of one content, composed through its page's content path pattern. An empty slug
    /// yields the page route itself - the single content of a home or "about" page.
    /// </summary>
    public virtual string BuildContentUrl(SiteUrlContext context, Page page, Content content)
    {
        return context.BuildAbsolute(
            page.BuildContentPath(content.PublishTime, content.Slug),
            content.CultureName);
    }

    /// <summary>
    /// The absolute URL of a page's feed in one language and format. Exposed so the
    /// <c>&lt;link rel="alternate"&gt;</c> autodiscovery tags (#13/#16/#17/#20) have one thing to call
    /// rather than re-deriving these paths.
    /// </summary>
    public virtual string BuildFeedUrl(SiteUrlContext context, Page page, string cultureName, SiteFeedFormat format)
    {
        var route = page.Route == "/" ? "/" + FeedConsts.GetFileName(format) : page.Route + "/" + FeedConsts.GetFileName(format);

        return context.BuildAbsolute(route, cultureName);
    }

    /// <summary>
    /// The sitemap index. Not culture-prefixed: sitemap and robots are per-domain documents that live at
    /// the site root regardless of language, and the index covers every language at once.
    /// </summary>
    public virtual string BuildSitemapIndexUrl(SiteUrlContext context)
    {
        return context.BuildAbsolute(SitemapConsts.IndexPath, context.DefaultCultureName);
    }

    public virtual string BuildSitemapShardUrl(SiteUrlContext context, int shardNumber)
    {
        return context.BuildAbsolute(SitemapConsts.BuildShardPath(shardNumber), context.DefaultCultureName);
    }
}
