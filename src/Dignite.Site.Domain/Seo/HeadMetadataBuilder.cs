using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Routing;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;
using Volo.Abp.Ui.Branding;

namespace Dignite.Site.Seo;

/// <summary>
/// Everything that goes in one resolved route's <c>&lt;head&gt;</c> (总体设计 §5.3, §5.4, §5.5, §5.9 -
/// GitHub issues #13, #16, #17, #20), composed from a single already-resolved <see cref="RouteMatch"/>
/// rather than re-resolving a path itself - a Tier 0 renderer (#21) that already has a match in hand can
/// call this directly with no further routing lookup, and a Tier 1 caller resolves once and feeds the
/// result here.
/// <para>
/// Reuses rather than re-derives: title/description come from <c>ContentSummaryResolver</c>, canonical and
/// every other absolute URL from <c>SiteUrlBuilder</c>, the noindex signal from <c>NoIndexRecognizer</c> -
/// the exact services the sitemap (#15) and feed (#31) generators already share, so this never disagrees
/// with them about the same content.
/// </para>
/// </summary>
public class HeadMetadataBuilder : DomainService
{
    protected SiteUrlBuilder UrlBuilder { get; }

    protected NoIndexRecognizer NoIndexRecognizer { get; }

    protected ContentSummaryResolver SummaryResolver { get; }

    protected IPageRepository PageRepository { get; }

    protected IContentRepository ContentRepository { get; }

    protected JsonLdContentDataResolver JsonLdContentDataResolver { get; }

    protected IBrandingProvider BrandingProvider { get; }

    public HeadMetadataBuilder(
        SiteUrlBuilder urlBuilder,
        NoIndexRecognizer noIndexRecognizer,
        ContentSummaryResolver summaryResolver,
        IPageRepository pageRepository,
        IContentRepository contentRepository,
        JsonLdContentDataResolver jsonLdContentDataResolver,
        IBrandingProvider brandingProvider)
    {
        UrlBuilder = urlBuilder;
        NoIndexRecognizer = noIndexRecognizer;
        SummaryResolver = summaryResolver;
        PageRepository = pageRepository;
        ContentRepository = contentRepository;
        JsonLdContentDataResolver = jsonLdContentDataResolver;
        BrandingProvider = brandingProvider;
    }

    /// <param name="match">
    /// A resolved route. Must not be <see cref="RouteMatch.None"/> - the caller's next step for an
    /// unmatched path is the redirect table and then a 404, neither of which has a <c>&lt;head&gt;</c> to
    /// build (mirrors how <c>RoutingPublicAppService</c> never special-cases <c>None</c> either).
    /// </param>
    /// <param name="cultureName">
    /// The language this route is being viewed in. Read straight from <paramref name="match"/>'s content
    /// when there is one (its own <c>CultureName</c> is authoritative); needed as an explicit parameter
    /// only because a bare page match carries no language of its own.
    /// </param>
    /// <param name="includeUnpublished">
    /// Mirrors <c>SiteRouteResolver.ResolveAsync</c>'s own parameter of the same name. A caller that
    /// resolved a preview with this <see langword="true"/> gets <see cref="HeadMetadata.NoIndex"/> forced
    /// true here - the obligation <c>SiteRouteResolver</c>'s own XML doc places on its caller (总体设计
    /// §5.3, GitHub issue #16).
    /// </param>
    public virtual async Task<HeadMetadata> BuildAsync(
        RouteMatch match,
        string cultureName,
        bool includeUnpublished = false,
        CancellationToken cancellationToken = default)
    {
        var page = match.Page!;
        var content = match.Content;
        var contentType = match.ContentType;

        var context = await UrlBuilder.CreateContextAsync(cancellationToken);
        var asOf = Clock.Now;

        // Resolved once regardless of route kind: both the current content's own noindex check below and
        // the hreflang alternates' noindex filtering (for a content match) need it.
        var seoField = await NoIndexRecognizer.FindFieldAsync(cancellationToken);

        var canonicalUrl = content != null
            ? UrlBuilder.BuildContentUrl(context, page, content)
            : UrlBuilder.BuildPageUrl(context, page, cultureName);

        string title;
        string? description = null;
        string? ogImageUrl = null;
        var contentNoIndex = false;

        if (content != null)
        {
            var lookup = await SummaryResolver.CreateLookupAsync(page.Id, cancellationToken);
            var fallbackTitle = string.IsNullOrWhiteSpace(content.Slug) ? page.DisplayName : content.Slug;
            var summary = SummaryResolver.Resolve(content, lookup, fallbackTitle);
            title = summary.Title;
            description = summary.Summary;

            contentNoIndex = NoIndexRecognizer.IsNoIndex(content, seoField);
            ogImageUrl = ReadOgImage(content, seoField);
        }
        else
        {
            title = page.DisplayName;
        }

        // IsActive matters as much as IsHomePage here: FindHomePageAsync filters only on the flag, but a
        // deactivated page is not routable (SiteRouteResolver 404s it, and the sitemap drops it). An
        // x-default or a Home crumb pointing at a 404 is worse than having neither - a bad x-default
        // invalidates the whole hreflang cluster.
        var homePage = await PageRepository.FindHomePageAsync(cancellationToken: cancellationToken);
        if (homePage is { IsActive: false })
        {
            homePage = null;
        }

        var xDefaultUrl = homePage == null ? null : UrlBuilder.BuildPageUrl(context, homePage, context.DefaultCultureName);

        var hreflangAlternates = await BuildHreflangAlternatesAsync(
            page, content, cultureName, seoField, context, includeUnpublished, asOf, cancellationToken);

        var siteData = new JsonLdSiteData(
            BrandingProvider.AppName, BrandingProvider.LogoUrl, context.BaseUrl);

        // The content's own CultureName is authoritative when there is one; the requested language is only
        // a fallback for a bare page match, which carries no language of its own.
        var effectiveCultureName = content?.CultureName
                                   ?? (CultureNameNormalizer.TryNormalize(cultureName, out var normalized)
                                       ? normalized
                                       : context.DefaultCultureName);

        var breadcrumb = BuildBreadcrumb(match, title, canonicalUrl, homePage, context, effectiveCultureName);

        JsonLdContentData? contentData = content != null && contentType != null
            ? await JsonLdContentDataResolver.ResolveAsync(contentType, content, cancellationToken)
            : null;

        return new HeadMetadata(
            title,
            description,
            ogImageUrl,
            canonicalUrl,
            effectiveCultureName,
            includeUnpublished || contentNoIndex,
            hreflangAlternates,
            xDefaultUrl,
            siteData,
            breadcrumb,
            contentData);
    }

    /// <summary>
    /// Reads the SEO field's social-share image, the same fail-open-and-log shape as
    /// <c>NoIndexRecognizer.IsNoIndex</c>. There is no fallback beyond the field itself: unlike title and
    /// description, an image cannot be reasonably guessed from an arbitrary other field, so leaving it
    /// unset simply omits <c>og:image</c> rather than inventing a placeholder.
    /// </summary>
    protected virtual string? ReadOgImage(Content content, Field? seoField)
    {
        if (seoField == null)
        {
            return null;
        }

        try
        {
            var value = content.GetField(seoField.Name, new SeoFieldValue()).OgImage;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Content {ContentId}'s '{FieldName}' value could not be read as an SEO field value; omitting og:image.",
                content.Id, seoField.Name);
            return null;
        }
    }

    /// <summary>
    /// A content's alternates are its actual translation rows (总体设计 §2.4) - reciprocal and
    /// self-referencing by construction, since the current language is one of the rows this query returns.
    /// A <i>sibling</i> translation an author marked noindex is excluded, the same as
    /// <c>SitemapEntrySource</c> excludes it from the sitemap; the rendered content itself is never
    /// excluded, however noindexed it is, because a set that omits its own page is not self-referencing at
    /// all and search engines discard such a cluster wholesale - which would take the sibling languages'
    /// annotations down with it, a strictly worse outcome than one extra entry.
    /// <para>
    /// A bare page match has no translation rows to read (Page carries no <c>CultureName</c> of its own),
    /// so its language footprint instead mirrors <c>SitemapEntrySource.CollectForPageAsync</c>'s own rule:
    /// the default language always, plus any other language that actually has published content beneath
    /// the page - and the language actually being viewed right now, so the set is always self-referencing
    /// even when nothing has been published in it yet.
    /// </para>
    /// <para>
    /// Every candidate is filtered against <see cref="SiteUrlContext.EnabledCultureNames"/> last.
    /// <c>TryStripCulturePrefix</c> refuses to strip a prefix for a language the tenant does not serve, so
    /// advertising one would publish a URL this same site then 404s on - the drift
    /// <see cref="SiteUrlContext"/>'s own two-direction design exists to prevent.
    /// </para>
    /// </summary>
    protected virtual async Task<IReadOnlyList<HreflangAlternate>> BuildHreflangAlternatesAsync(
        Page page,
        Content? content,
        string cultureName,
        Field? seoField,
        SiteUrlContext context,
        bool includeUnpublished,
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        if (content != null)
        {
            var translations = await ContentRepository.GetTranslationsAsync(
                page.Id, content.ContentTypeId, content.Slug, cancellationToken);

            return translations
                .Where(c => c.Id == content.Id || includeUnpublished || c.IsPublished(asOf))
                .Where(c => c.Id == content.Id || includeUnpublished || !NoIndexRecognizer.IsNoIndex(c, seoField))
                .Where(c => IsServed(context, c.CultureName))
                .Select(c => new HreflangAlternate(c.CultureName, UrlBuilder.BuildContentUrl(context, page, c)))
                .ToList();
        }

        var cultureNames = await ContentRepository.GetDistinctCultureNamesAsync(
            page.Id,
            status: includeUnpublished ? null : ContentStatus.Published,
            publishedBefore: includeUnpublished ? null : asOf,
            cancellationToken: cancellationToken);

        var cultures = new List<string> { context.DefaultCultureName };

        if (CultureNameNormalizer.TryNormalize(cultureName, out var normalizedCurrent)
            && IsServed(context, normalizedCurrent)
            && !cultures.Contains(normalizedCurrent, StringComparer.Ordinal))
        {
            cultures.Add(normalizedCurrent);
        }

        cultures.AddRange(
            cultureNames
                .Where(c => IsServed(context, c))
                .Where(c => !cultures.Contains(c, StringComparer.Ordinal))
                .OrderBy(c => c, StringComparer.Ordinal));

        return cultures
            .Select(culture => new HreflangAlternate(culture, UrlBuilder.BuildPageUrl(context, page, culture)))
            .ToList();
    }

    /// <summary>
    /// Whether this site actually serves <paramref name="cultureName"/>. The default language is always
    /// served - it is the first entry of the enabled list by construction - and is checked explicitly only
    /// so a caller cannot depend on list membership alone.
    /// </summary>
    private static bool IsServed(SiteUrlContext context, string cultureName)
    {
        return string.Equals(cultureName, context.DefaultCultureName, StringComparison.Ordinal)
               || context.EnabledCultureNames.Contains(cultureName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Home first, this route last.
    /// <para>
    /// Collapses to Home alone when this route <i>is</i> the home page's own URL (a bare page or
    /// empty-slug match on the home page - a second node repeating that URL would say nothing new). A
    /// real <see cref="RouteMatchKind.Content"/> living directly under the home page is a different URL
    /// even though its <see cref="Page"/> is the home page, so it still gets its own crumb - only the
    /// intermediate "page" crumb is skipped in that case, since the Home crumb already represents it.
    /// </para>
    /// </summary>
    protected virtual IReadOnlyList<JsonLdBreadcrumbItem> BuildBreadcrumb(
        RouteMatch match,
        string title,
        string canonicalUrl,
        Page? homePage,
        SiteUrlContext context,
        string effectiveCultureName)
    {
        var items = new List<JsonLdBreadcrumbItem>();

        // The "Home" crumb links to the home page in the language actually being viewed, not necessarily
        // the default culture that x-default (a separate SEO concept) always points at.
        if (homePage != null)
        {
            items.Add(new JsonLdBreadcrumbItem(
                homePage.DisplayName, UrlBuilder.BuildPageUrl(context, homePage, effectiveCultureName)));
        }

        var isHomePage = homePage != null && homePage.Id == match.Page!.Id;

        if (match.Kind == RouteMatchKind.Content)
        {
            if (!isHomePage)
            {
                // A real detail beneath its own list page: that page gets its own crumb first.
                items.Add(new JsonLdBreadcrumbItem(
                    match.Page!.DisplayName, UrlBuilder.BuildPageUrl(context, match.Page, effectiveCultureName)));
            }

            items.Add(new JsonLdBreadcrumbItem(title, canonicalUrl));
        }
        else if (!isHomePage)
        {
            // Page or ContentOfPage, not the home page itself: this route already collapses to the page's
            // own URL, one crumb for it.
            items.Add(new JsonLdBreadcrumbItem(match.Page!.DisplayName, canonicalUrl));
        }

        return items;
    }
}
