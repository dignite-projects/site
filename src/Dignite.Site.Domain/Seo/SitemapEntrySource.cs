using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace Dignite.Site.Seo;

/// <summary>
/// Enumerates every URL this site wants indexed, derived entirely from the Page + Content routing table
/// (总体设计 §5.2, GitHub issue #15): every base page route, plus every published content expanded through
/// its page's content path pattern.
/// <para>
/// Serialization lives one layer up; this class only decides <i>which</i> URLs exist and <i>when</i> each
/// last changed, which is the part with all the reasoning in it.
/// </para>
/// <para>
/// <b>lastmod is the whole ballgame, and it is a trap.</b> If every URL reports today's date, Google stops
/// trusting the field across the <i>entire</i> sitemap - the cost of getting it wrong is the whole site's
/// worth of the signal, not one row's. So it is computed exclusively from stored columns:
/// <c>max(LastModificationTime ?? CreationTime, PublishTime)</c>. Both terms are written by an actual
/// edit, never by a generation run, which also means a scheduled publish arriving does not move it - there
/// is no background job touching rows at publish time (GitHub issue #6), and the value stays the stored
/// <c>PublishTime</c> either way.
/// </para>
/// </summary>
public class SitemapEntrySource : DomainService
{
    protected IPageRepository PageRepository { get; }

    protected IContentRepository ContentRepository { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    protected NoIndexRecognizer NoIndexRecognizer { get; }

    public SitemapEntrySource(
        IPageRepository pageRepository,
        IContentRepository contentRepository,
        SiteUrlBuilder urlBuilder,
        NoIndexRecognizer noIndexRecognizer)
    {
        PageRepository = pageRepository;
        ContentRepository = contentRepository;
        UrlBuilder = urlBuilder;
        NoIndexRecognizer = noIndexRecognizer;
    }

    /// <summary>
    /// Every indexable URL, ordered so that one page's URLs stay contiguous - which is what gives the
    /// sharding its per-type grouping without shard names having to encode a page (总体设计 §5.2).
    /// </summary>
    public virtual async Task<List<SitemapEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        var context = await UrlBuilder.CreateContextAsync(cancellationToken);

        // Resolved once for the whole run, not per content - the exact reason NoIndexRecognizer splits
        // the lookup from the check.
        var seoField = await NoIndexRecognizer.FindFieldAsync(cancellationToken);
        var asOf = Clock.Now;

        var collected = new List<SitemapEntry>();

        // A URL an author marked noindex has to disappear even if something else also claims it. The
        // "about" page is the case that makes this necessary: its single empty-slug content is reached at
        // the page's own route, so excluding only the content would leave the page URL behind and the
        // noindex would have achieved nothing.
        var excluded = new HashSet<string>(StringComparer.Ordinal);

        // Inactive pages are already filtered out by the repository - an inactive page is not routable and
        // contributes nothing here.
        var pages = (await PageRepository.GetRoutableListAsync(cancellationToken))
            .OrderBy(p => p.Route, StringComparer.Ordinal)
            .ToList();

        foreach (var page in pages)
        {
            collected.AddRange(await CollectForPageAsync(page, context, seoField, asOf, excluded, cancellationToken));
        }

        return Consolidate(collected, excluded);
    }

    protected virtual async Task<List<SitemapEntry>> CollectForPageAsync(
        Page page,
        SiteUrlContext context,
        Fields.Field? seoField,
        DateTime asOf,
        HashSet<string> excluded,
        CancellationToken cancellationToken)
    {
        // Published-and-arrived is the single definition the router and the public list queries also use,
        // so drafts, archived rows and not-yet-due scheduled rows all drop out here with no second opinion.
        var contents = await ContentRepository.GetListAsync(
            pageId: page.Id,
            status: ContentStatus.Published,
            publishedBefore: asOf,
            sorting: $"{nameof(Content.CultureName)},{nameof(Content.Slug)}",
            cancellationToken: cancellationToken);

        var contentEntries = new List<SitemapEntry>();
        var newestByCulture = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        foreach (var content in contents)
        {
            // A language the tenant no longer serves cannot be routed back (SiteUrlContext.IsServed), so
            // its URLs are dropped before they are ever claimed - not added to the exclusion set, since
            // that set is for URLs a noindex has to retract from an otherwise valid claim.
            if (!context.IsServed(content.CultureName))
            {
                continue;
            }

            var location = UrlBuilder.BuildContentUrl(context, page, content);

            if (NoIndexRecognizer.IsNoIndex(content, seoField))
            {
                excluded.Add(location);
                continue;
            }

            var lastModified = GetLastModified(content);
            contentEntries.Add(new SitemapEntry(location, lastModified, SitemapConsts.ContentPriority));

            if (!newestByCulture.TryGetValue(content.CultureName, out var newest) || lastModified > newest)
            {
                newestByCulture[content.CultureName] = lastModified;
            }
        }

        // The page's own route exists in the default language whether or not anything was written in it -
        // an active page is a real, routable URL - and in every other language that both has content under
        // it and is one the tenant actually serves.
        var pageCultures = new List<string> { context.DefaultCultureName };
        pageCultures.AddRange(
            contents.Select(c => c.CultureName)
                .Distinct(StringComparer.Ordinal)
                .Where(c => !string.Equals(c, context.DefaultCultureName, StringComparison.Ordinal))
                .Where(context.IsServed)
                .OrderBy(c => c, StringComparer.Ordinal));

        var pageOwnLastModified = page.LastModificationTime ?? page.CreationTime;
        var pagePriority = page.IsHomeRoute() ? SitemapConsts.HomePagePriority : SitemapConsts.PagePriority;

        var pageEntries = pageCultures
            .Select(culture => new SitemapEntry(
                UrlBuilder.BuildPageUrl(context, page, culture),
                // A list page genuinely changes when a post appears under it, so its lastmod is the newest
                // of what it lists - falling back to the page row's own timestamp when it lists nothing.
                newestByCulture.TryGetValue(culture, out var newest) && newest > pageOwnLastModified
                    ? newest
                    : pageOwnLastModified,
                pagePriority))
            .ToList();

        // Page routes before the contents beneath them, so a shard reads top-down.
        pageEntries.AddRange(contentEntries);
        return pageEntries;
    }

    /// <summary>
    /// Drops excluded URLs and folds duplicates together. Done in one pass at the end rather than while
    /// collecting, because a URL can be excluded <i>after</i> it has already been claimed.
    /// </summary>
    protected virtual List<SitemapEntry> Consolidate(List<SitemapEntry> collected, HashSet<string> excluded)
    {
        var seen = new Dictionary<string, SitemapEntry>(StringComparer.Ordinal);
        var ordered = new List<SitemapEntry>(collected.Count);

        foreach (var entry in collected)
        {
            if (excluded.Contains(entry.Location))
            {
                continue;
            }

            if (seen.TryGetValue(entry.Location, out var existing))
            {
                existing.Merge(entry);
                continue;
            }

            seen.Add(entry.Location, entry);
            ordered.Add(entry);
        }

        return ordered;
    }

    /// <summary>
    /// When this content genuinely last changed - see <see cref="ContentModificationTime.Of"/>, which the
    /// feeds share so both documents date the same content identically.
    /// </summary>
    protected virtual DateTime GetLastModified(Content content)
    {
        return ContentModificationTime.Of(content);
    }
}
