using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Files;
using Dignite.Site.Pages;
using Dignite.Site.Routing;
using Dignite.Site.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

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

    protected IContentRepository ContentRepository { get; }

    // Resolved lazily rather than through the constructor, so that a host's class derived from this one keeps
    // compiling: both are used only by the share-image default, the site name and the breadcrumb.
    protected IPageRepository PageRepository => LazyServiceProvider.LazyGetRequiredService<IPageRepository>();

    protected ISettingProvider SettingProvider => LazyServiceProvider.LazyGetRequiredService<ISettingProvider>();

    protected IContentTypeRepository ContentTypeRepository => LazyServiceProvider.LazyGetRequiredService<IContentTypeRepository>();

    public HeadMetadataBuilder(
        SiteUrlBuilder urlBuilder,
        NoIndexRecognizer noIndexRecognizer,
        ContentSummaryResolver summaryResolver,
        IContentRepository contentRepository)
    {
        UrlBuilder = urlBuilder;
        NoIndexRecognizer = noIndexRecognizer;
        SummaryResolver = summaryResolver;
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

        // A filtered view - Kind is Page, FilterValues non-empty - comes in two kinds, told apart by
        // RouteMatch.IsTruncated.
        //
        // A truncated one (PageRoute.TryMatchPartial - /news/2026-07 read off
        // /news/{publishTime:yyyy-MM}/{slug}) is the platform guessing at an address no route declares, so
        // it has no URL of its own to build a canonicalUrl from. Reusing the bare-address canonical and
        // hreflang set without also forcing noindex would tell search engines that the filtered view IS the
        // canonical page - the standard faceted-navigation fix is exactly what a bare match already gets for
        // free (canonical -> the unfiltered page) plus noindex, so the filtered variant never competes with
        // it in results while still being crawlable for its links.
        //
        // A declared one (PageRoute.TryMatchExact - /news/tutorials against
        // /news/{category?:...}/{publishTime?:yyyy:...}) is an address the page's own route spells out, as
        // real as any other: it is canonical at itself (总体设计 §5.3 - canonical is self-referencing, derived
        // from the route's structure) and indexable, a category or archive landing page in its own right.
        // Except when one of its date filters cannot actually filter anything (see HasUnusableDateFilter):
        // the list it renders is then the unfiltered one, a duplicate of the bare address, and it falls back
        // to the truncated treatment.
        var isFilteredView = content == null && match.FilterValues.Count > 0;
        var isDeclaredFilteredView = isFilteredView && !match.IsTruncated && !HasUnusableDateFilter(match.FilterValues);

        var canonicalUrl = content != null
            ? UrlBuilder.BuildContentUrl(context, page, content)
            : isDeclaredFilteredView
                ? UrlBuilder.BuildFilteredPageUrl(context, page, match.FilterValues, cultureName)
                : UrlBuilder.BuildPageUrl(context, page, cultureName);

        string title;
        string baseTitle;
        string? description = null;
        string? ogImageUrl = null;
        var contentNoIndex = false;

        if (content != null)
        {
            var lookup = await SummaryResolver.CreateLookupAsync(page.Id, cancellationToken);
            var fallbackTitle = string.IsNullOrWhiteSpace(content.Slug) ? page.DisplayName : content.Slug;
            var summary = SummaryResolver.Resolve(content, lookup, fallbackTitle);
            title = summary.Title;
            baseTitle = title;
            description = summary.Summary;

            contentNoIndex = NoIndexRecognizer.IsNoIndex(content, seoField);
            ogImageUrl = ReadOgImage(content, seoField, context);
        }
        else
        {
            // No content matched, but the page may still carry its own empty-slug content in this language -
            // the one its bare address resolves to (RouteMatch.ForContentOfPage). A filtered view of that page
            // (/blog/engineering against /blog/{category?:...}) is the same page narrowed down, so that content
            // is the base of its <head>: the title, description, og:image and noindex its author wrote for the
            // page, in the language being viewed - rather than Page.DisplayName, which is one admin-facing
            // label for every language. It is only the base: canonical and hreflang above stay the filtered
            // view's own, since pointing them at the content's own URL would declare every filtered view a
            // duplicate of the bare page.
            var pageContent = await FindPageContentAsync(page, cultureName, includeUnpublished, asOf, cancellationToken);
            baseTitle = page.DisplayName;

            if (pageContent != null)
            {
                var lookup = await SummaryResolver.CreateLookupAsync(page.Id, cancellationToken);
                var summary = SummaryResolver.Resolve(pageContent, lookup, page.DisplayName);
                baseTitle = summary.Title;
                description = summary.Summary;

                contentNoIndex = NoIndexRecognizer.IsNoIndex(pageContent, seoField);
                ogImageUrl = ReadOgImage(pageContent, seoField, context);
            }

            // A declared filtered view is indexable, so it needs a title of its own - every category and
            // archive of one page would otherwise share the page's, one duplicate title per filtered view. The
            // captured values, in the order the route names them, are the only per-view text there is to add.
            // They are the raw values ("engineering", not "研发"): this runs API-side, where a template's own
            // texts are out of reach - baseTitle is returned alongside so the template can compose a
            // localized title itself (GitHub issue #73), with this one as the fallback when it doesn't.
            title = isDeclaredFilteredView
                ? $"{baseTitle} - {string.Join(" / ", match.FilterValues.Values)}"
                : baseTitle;
        }

        var hreflangAlternates = await BuildHreflangAlternatesAsync(
            page, content, cultureName, seoField, context, includeUnpublished, asOf,
            isDeclaredFilteredView ? match.FilterValues : null, cancellationToken);

        var xDefaultUrl = SelectXDefaultUrl(hreflangAlternates, context);

        // The site's default share image, for a page whose author chose none (Site.Seo.DefaultShareImage).
        var isDefaultOgImage = false;
        if (ogImageUrl == null)
        {
            ogImageUrl = await ReadDefaultOgImageAsync(context);
            isDefaultOgImage = ogImageUrl != null;
        }

        var breadcrumb = await BuildBreadcrumbAsync(
            page, content, title, baseTitle, canonicalUrl, cultureName, context, includeUnpublished, asOf, cancellationToken);

        // The content's own CultureName is authoritative when there is one; the requested language is only
        // a fallback for a bare page match, which carries no language of its own.
        var effectiveCultureName = content?.CultureName
                                   ?? (CultureNameNormalizer.TryNormalize(cultureName, out var normalized)
                                       ? normalized
                                       : context.DefaultCultureName);

        var siteName = await SettingProvider.GetOrNullAsync(SiteSettings.Branding.AppName);

        return new HeadMetadata(
            title,
            baseTitle,
            description,
            ogImageUrl,
            canonicalUrl,
            effectiveCultureName,
            includeUnpublished || contentNoIndex || (isFilteredView && !isDeclaredFilteredView),
            hreflangAlternates,
            xDefaultUrl)
        {
            IsDefaultOgImage = isDefaultOgImage,
            // Only an image of the file library is served at a known size: it is cropped to it (SiteFileUrl.Sized).
            OgImageWidth = SiteFileUrl.IsSiteFile(ogImageUrl) ? OpenGraphConsts.ImageWidth : null,
            OgImageHeight = SiteFileUrl.IsSiteFile(ogImageUrl) ? OpenGraphConsts.ImageHeight : null,
            SiteName = string.IsNullOrWhiteSpace(siteName) ? null : siteName.Trim(),
            BaseUrl = context.BaseUrl,
            Breadcrumb = breadcrumb
        };
    }

    /// <summary>
    /// <c>Site.Seo.DefaultShareImage</c> as an <c>og:image</c>: sized and made absolute the way the SEO field's own
    /// image is (<see cref="ReadOgImage"/>). Null when the setting is empty.
    /// </summary>
    protected virtual async Task<string?> ReadDefaultOgImageAsync(SiteUrlContext context)
    {
        var value = await SettingProvider.GetOrNullAsync(SiteSettings.Seo.DefaultShareImage);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : SiteFileUrl.Absolutize(
                SiteFileUrl.Sized(value.Trim(), OpenGraphConsts.ImageWidth, OpenGraphConsts.ImageHeight),
                context.BaseUrl);
    }

    /// <summary>
    /// <see cref="HeadMetadata.Breadcrumb"/>: the page's ancestors by <see cref="Page.ParentId"/>, with the home page
    /// in front when it is not one of them, then the page, then the content.
    /// <para>
    /// Each page is named the way its own head is titled, in the language being viewed: the title of its own
    /// empty-slug content when that is visible, else its <see cref="Page.DisplayName"/>. The page being rendered
    /// is named by <paramref name="baseTitle"/> - a filtered view's title without the filter values, since the
    /// step stands for the page, not for the view - and a content by <paramref name="title"/>. The page's own
    /// content (empty slug) is the page's step, not one more. An inactive
    /// ancestor has no address a visitor could follow and is left out. A page tree is a few levels deep, so the
    /// ancestors and their own contents are read one by one; what names a content's title - the content types of the
    /// pages and the field library - is read once for all the steps, since the head of every page asks for it.
    /// </para>
    /// </summary>
    protected virtual async Task<IReadOnlyList<BreadcrumbItem>> BuildBreadcrumbAsync(
        Page page,
        Content? content,
        string title,
        string baseTitle,
        string canonicalUrl,
        string cultureName,
        SiteUrlContext context,
        bool includeUnpublished,
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        var chain = new List<Page> { page };
        var seen = new HashSet<Guid> { page.Id };
        var parentId = page.ParentId;
        // A parent that no longer exists ends the walk, and so does a cycle, which the page manager refuses but an
        // edit straight in the database could still make.
        while (parentId.HasValue && seen.Add(parentId.Value))
        {
            var parent = await PageRepository.FindAsync(parentId.Value, includeDetails: false, cancellationToken);
            if (parent == null)
            {
                break;
            }

            chain.Add(parent);
            parentId = parent.ParentId;
        }

        chain.Reverse();

        if (!chain[0].IsHomeRoute())
        {
            var home = await PageRepository.FindHomePageAsync(includeDetails: false, cancellationToken);
            if (home != null && home.IsActive && !seen.Contains(home.Id))
            {
                chain.Insert(0, home);
            }
        }

        // A page's own content - the one its bare address shows, with an empty slug - is the page itself, not a
        // step below it.
        var isPageContent = content != null && string.IsNullOrEmpty(content.Slug);

        // Each step with the name it already has (the page being rendered), or the page content that will name it.
        var steps = new List<(Page Page, string? Name, Content? PageContent)>();
        foreach (var step in chain)
        {
            var isCurrentPage = step.Id == page.Id;
            if (!isCurrentPage && !step.IsActive)
            {
                continue;
            }

            if (isCurrentPage && content == null)
            {
                steps.Add((step, baseTitle, null));
            }
            else if (isCurrentPage && isPageContent)
            {
                steps.Add((step, title, null));
            }
            else
            {
                steps.Add((step, null, await FindPageContentAsync(step, cultureName, includeUnpublished, asOf, cancellationToken)));
            }
        }

        // One lookup for every step that has a content of its own: a content type's id keys its entry, so the content
        // types of several pages share one, and the field library is read once.
        ContentSummaryLookup? lookup = null;
        var pagesWithContent = steps.Where(s => s.PageContent != null).Select(s => s.Page.Id).Distinct().ToList();
        if (pagesWithContent.Count > 0)
        {
            var contentTypes = new List<ContentType>();
            foreach (var pageId in pagesWithContent)
            {
                contentTypes.AddRange(await ContentTypeRepository.GetListByPageAsync(pageId, cancellationToken));
            }

            lookup = await SummaryResolver.CreateLookupAsync(contentTypes, cancellationToken);
        }

        var items = new List<BreadcrumbItem>();
        foreach (var (step, knownName, pageContent) in steps)
        {
            var name = knownName
                       ?? (pageContent != null
                           ? SummaryResolver.Resolve(pageContent, lookup!, step.DisplayName).Title
                           : step.DisplayName);

            items.Add(new BreadcrumbItem(
                name,
                UrlBuilder.BuildPagePath(context, step, cultureName),
                UrlBuilder.BuildPageUrl(context, step, cultureName),
                step.Name));
        }

        if (content != null && !isPageContent)
        {
            items.Add(new BreadcrumbItem(title, UrlBuilder.BuildContentPath(context, page, content), canonicalUrl, page.Name, content.Slug));
        }

        return items;
    }

    /// <summary>
    /// The <c>x-default</c> of one hreflang cluster: the same page in the default language, which is also
    /// what a visitor gets at that page's unprefixed address when none of the listed languages fits - the
    /// default language is the one without a prefix (总体设计 §5.5), so there is no language-neutral page
    /// elsewhere for it to point at. Taken from the cluster itself, so it is never a URL outside it: an
    /// annotation pointing at a different page is not reciprocated by that page and is dropped by search
    /// engines. Null for a cluster of one - there is no choice for x-default to break - and for one with
    /// no default-language member, e.g. content never translated into the default language.
    /// </summary>
    protected virtual string? SelectXDefaultUrl(IReadOnlyList<HreflangAlternate> alternates, SiteUrlContext context)
    {
        if (alternates.Count < 2)
        {
            return null;
        }

        return alternates.FirstOrDefault(a => string.Equals(a.CultureName, context.DefaultCultureName, StringComparison.Ordinal))?.Url;
    }

    /// <summary>
    /// The page's own empty-slug content in <paramref name="cultureName"/>, under the same visibility rule
    /// <c>SiteRouteResolver</c> applies when it resolves the page's bare address to that content - so a
    /// filtered view never takes its <c>&lt;head&gt;</c> from a content its bare address would not show.
    /// </summary>
    protected virtual async Task<Content?> FindPageContentAsync(
        Page page,
        string cultureName,
        bool includeUnpublished,
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        var culture = CultureNameNormalizer.TryNormalize(cultureName, out var normalized) ? normalized : cultureName;
        var pageContent = await ContentRepository.FindBySlugAsync(page.Id, culture, string.Empty, cancellationToken);

        return pageContent != null && (includeUnpublished || pageContent.IsPubliclyAccessible(asOf))
            ? pageContent
            : null;
    }

    /// <summary>
    /// Whether any date-shaped group of <paramref name="filterValues"/> - every FORMAT-bearing entry
    /// sharing one name, read together the way <c>SiteRenderFilterValueMapper</c> and
    /// <c>ContentListTagHelper</c> read them - fails to form a complete period (a month with no year,
    /// <c>/news/08</c>), which those two then drop instead of filtering on. A group whose formats name no
    /// date unit at all is some other kind of format, and none of this class's business. Only the platform's
    /// own date handling is second-guessed here; whether a template actually lists anything filtered by a
    /// business field is the template's own affair, the same as it always was.
    /// </summary>
    protected virtual bool HasUnusableDateFilter(IReadOnlyDictionary<string, string> filterValues)
    {
        var dateGroups = filterValues
            .Select(entry => (Key: entry.Key, Value: entry.Value, Colon: entry.Key.IndexOf(':')))
            .Where(entry => entry.Colon > 0)
            .Select(entry => (Name: entry.Key[..entry.Colon], Format: entry.Key[(entry.Colon + 1)..], entry.Value))
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Any(entry => RoutePlaceholderDateFormat.IsDateFormat(entry.Format)));

        return dateGroups.Any(group =>
            !RoutePlaceholderDateFormat.TryGetRange(group.Select(entry => (entry.Format, entry.Value)), out _, out _));
    }

    /// <summary>
    /// Reads the SEO field's social-share image, the same fail-open-and-log shape as
    /// <c>NoIndexRecognizer.IsNoIndex</c>. There is no fallback beyond the field itself: unlike title and
    /// description, an image cannot be reasonably guessed from an arbitrary other field, so leaving it
    /// unset simply omits <c>og:image</c> rather than inventing a placeholder.
    /// <para>
    /// An image from the site's file library is asked for at <see cref="OpenGraphConsts"/>' size (GitHub
    /// issue #72) - the field stores the original upload, and the public read endpoint crops it on request
    /// (<see cref="SiteFileUrl"/>). An external address is
    /// returned as written: nothing here can resize it.
    /// </para>
    /// <para>
    /// <c>og:image</c> has to be absolute, and a file-library address is stored relative, so a relative value
    /// is put on the site's primary domain (<see cref="SiteUrlContext.BaseUrl"/>, the same origin as the
    /// canonical URL) - the public site serves <c>/api/site-public/files/</c> itself.
    /// </para>
    /// </summary>
    protected virtual string? ReadOgImage(Content content, Field? seoField, SiteUrlContext context)
    {
        if (seoField == null)
        {
            return null;
        }

        try
        {
            var value = content.GetField(seoField.Name, new SeoFieldValue()).OgImage;
            return string.IsNullOrWhiteSpace(value)
                ? null
                : SiteFileUrl.Absolutize(
                    SiteFileUrl.Sized(value, OpenGraphConsts.ImageWidth, OpenGraphConsts.ImageHeight),
                    context.BaseUrl);
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
    /// A declared filtered view (<paramref name="declaredFilterValues"/> non-null) uses that same language
    /// footprint, each alternate pointing at the same filtered path in its own language rather than at the
    /// bare page - the set has to reference the page actually being rendered, or it is not
    /// self-referencing at all.
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
        IReadOnlyDictionary<string, string>? declaredFilterValues,
        CancellationToken cancellationToken)
    {
        if (content != null)
        {
            var translations = await ContentRepository.GetTranslationsAsync(
                page.Id, content.ContentTypeId, content.Slug, cancellationToken);

            return translations
                .Where(c => c.Id == content.Id || includeUnpublished || c.IsPubliclyAccessible(asOf))
                .Where(c => c.Id == content.Id || includeUnpublished || !NoIndexRecognizer.IsNoIndex(c, seoField))
                .Where(c => context.IsServed(c.CultureName))
                .Select(c => new HreflangAlternate(
                    c.CultureName,
                    UrlBuilder.BuildContentUrl(context, page, c),
                    UrlBuilder.BuildContentPath(context, page, c)))
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
            .Select(culture => declaredFilterValues != null
                ? new HreflangAlternate(
                    culture,
                    UrlBuilder.BuildFilteredPageUrl(context, page, declaredFilterValues, culture),
                    UrlBuilder.BuildFilteredPagePath(context, page, declaredFilterValues, culture))
                : new HreflangAlternate(
                    culture,
                    UrlBuilder.BuildPageUrl(context, page, culture),
                    UrlBuilder.BuildPagePath(context, page, culture)))
            .ToList();
    }
}
