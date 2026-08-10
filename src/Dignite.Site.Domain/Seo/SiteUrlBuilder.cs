using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Seo;

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

    protected SiteUrlContextCache RequestCache { get; }

    public SiteUrlBuilder(
        ISiteBaseUrlResolver baseUrlResolver, SiteLanguageProvider languageProvider, SiteUrlContextCache requestCache)
    {
        BaseUrlResolver = baseUrlResolver;
        LanguageProvider = languageProvider;
        RequestCache = requestCache;
    }

    /// <exception cref="PrimaryDomainNotConfiguredException">
    /// No base URL is configured and none could be inferred.
    /// </exception>
    public virtual async Task<SiteUrlContext> CreateContextAsync(CancellationToken cancellationToken = default)
    {
        return await RequestCache.GetOrCreateAsync(() => CreateContextCoreAsync(cancellationToken));
    }

    protected virtual async Task<SiteUrlContext> CreateContextCoreAsync(CancellationToken cancellationToken)
    {
        var baseUrl = await BaseUrlResolver.GetBaseUrlAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new PrimaryDomainNotConfiguredException();
        }

        var languages = await LanguageProvider.GetEnabledLanguagesAsync(cancellationToken);

        return new SiteUrlContext(baseUrl!, languages[0], languages);
    }

    /// <summary>The absolute URL of a page's own address - its list view, or its single content.</summary>
    public virtual string BuildPageUrl(SiteUrlContext context, Page page, string cultureName)
    {
        return context.BuildAbsolute(page.GetPath(), cultureName);
    }

    /// <summary>
    /// The absolute URL of one content, composed through its page's route template. An empty slug yields
    /// the page's own address - the single content of a home or "about" page.
    /// </summary>
    public virtual string BuildContentUrl(SiteUrlContext context, Page page, Content content)
    {
        return context.BuildAbsolute(page.BuildContentPath(content), content.CultureName);
    }

    /// <summary>
    /// The relative, culture-prefixed path of one content beneath its page - e.g. <c>/blog/my-trip</c> or
    /// <c>/zh-Hans/blog/my-trip</c>. The same address <see cref="BuildContentUrl"/> makes absolute, for a
    /// caller that wants a same-origin <c>&lt;a href&gt;</c> instead - correct behind a reverse proxy
    /// where this tenant's configured/inferred <c>BaseUrl</c> may not be the host the browser is actually
    /// talking to.
    /// </summary>
    public virtual string BuildContentPath(SiteUrlContext context, Page page, Content content)
    {
        return context.ApplyCulturePrefix(page.BuildContentPath(content), content.CultureName);
    }

    /// <summary>
    /// The absolute URL of a page's feed in one language and format. Exposed so the
    /// <c>&lt;link rel="alternate"&gt;</c> autodiscovery tags (#13/#16/#17/#20) have one thing to call
    /// rather than re-deriving these paths.
    /// </summary>
    public virtual string BuildFeedUrl(SiteUrlContext context, Page page, string cultureName, SiteFeedFormat format)
    {
        var path = page.GetPath();
        var route = path == "/" ? "/" + FeedConsts.GetFileName(format) : path + "/" + FeedConsts.GetFileName(format);

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
