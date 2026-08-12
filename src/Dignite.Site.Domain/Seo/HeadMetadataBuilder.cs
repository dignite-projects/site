using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Routing;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Seo;

/// <summary>
/// Everything that goes in one resolved route's <c>&lt;head&gt;</c> (总体设计 §5.3, §5.5, §5.9 - GitHub
/// issues #13, #16, #17), composed from a single already-resolved <see cref="RouteMatch"/> rather than
/// re-resolving a path itself - a Tier 0 renderer (#21) that already has a match in hand can call this
/// directly with no further routing lookup, and a Tier 1 caller resolves once and feeds the result here.
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

    public HeadMetadataBuilder(
        SiteUrlBuilder urlBuilder,
        NoIndexRecognizer noIndexRecognizer,
        ContentSummaryResolver summaryResolver,
        IPageRepository pageRepository,
        IContentRepository contentRepository)
    {
        UrlBuilder = urlBuilder;
        NoIndexRecognizer = noIndexRecognizer;
        SummaryResolver = summaryResolver;
        PageRepository = pageRepository;
        ContentRepository = contentRepository;
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

        // A partial match (SiteRouteResolver.TryMatchPartial - Kind is Page, FilterValues non-empty) has no
        // URL of its own to build a canonicalUrl from: the page's own bare address is the only address
        // PageRoute can render, yet the request actually named some of the page's placeholders, e.g.
        // /news/2026-07 against /news/{publishTime:yyyy-MM}/{slug}. Reusing the bare-address canonical and
        // hreflang set here without also forcing noindex would tell search engines that the filtered view
        // IS the canonical page - the standard faceted-navigation fix is exactly what a bare match already
        // gets for free (canonical -> the unfiltered page) plus noindex, so the filtered variant itself
        // never competes with it in results while still being crawlable for its links.
        var isPartialMatch = content == null && match.FilterValues.Count > 0;

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

        // IsActive matters as much as being the home route here: FindHomePageAsync judges Route alone,
        // but a deactivated page is not routable (SiteRouteResolver 404s it, and the sitemap drops it).
        // An x-default or a Home crumb pointing at a 404 is worse than having neither - a bad x-default
        // invalidates the whole hreflang cluster.
        var homePage = await PageRepository.FindHomePageAsync(cancellationToken: cancellationToken);
        if (homePage is { IsActive: false })
        {
            homePage = null;
        }

        var xDefaultUrl = homePage == null ? null : UrlBuilder.BuildPageUrl(context, homePage, context.DefaultCultureName);

        var hreflangAlternates = await BuildHreflangAlternatesAsync(
            page, content, cultureName, seoField, context, includeUnpublished, asOf, cancellationToken);

        // The content's own CultureName is authoritative when there is one; the requested language is only
        // a fallback for a bare page match, which carries no language of its own.
        var effectiveCultureName = content?.CultureName
                                   ?? (CultureNameNormalizer.TryNormalize(cultureName, out var normalized)
                                       ? normalized
                                       : context.DefaultCultureName);

        return new HeadMetadata(
            title,
            description,
            ogImageUrl,
            canonicalUrl,
            effectiveCultureName,
            includeUnpublished || contentNoIndex || isPartialMatch,
            hreflangAlternates,
            xDefaultUrl);
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
                .Where(c => context.IsServed(c.CultureName))
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
            && context.IsServed(normalizedCurrent)
            && !cultures.Contains(normalizedCurrent, StringComparer.Ordinal))
        {
            cultures.Add(normalizedCurrent);
        }

        cultures.AddRange(
            cultureNames
                .Where(context.IsServed)
                .Where(c => !cultures.Contains(c, StringComparer.Ordinal))
                .OrderBy(c => c, StringComparer.Ordinal));

        return cultures
            .Select(culture => new HreflangAlternate(culture, UrlBuilder.BuildPageUrl(context, page, culture)))
            .ToList();
    }
}
