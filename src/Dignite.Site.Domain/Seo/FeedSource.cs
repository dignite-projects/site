using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace Dignite.Site.Seo;

/// <summary>
/// The items of one page's feed, in one language (总体设计 §5.9, GitHub issue #31).
/// <para>
/// Same data source as the sitemap - published contents under a page - and the same exclusions, so the two
/// documents cannot advertise different sets. A feed is the "what changed recently" view of that data:
/// newest first, capped at <see cref="FeedConsts.MaxItems"/>, where the sitemap is the exhaustive one.
/// </para>
/// </summary>
public class FeedSource : DomainService
{
    /// <summary>
    /// How many of a page's most recent contents are examined at most while paging for
    /// <see cref="FeedConsts.MaxItems"/> non-noindexed ones. Bounds the worst case - a page whose recent
    /// history is almost entirely noindexed - to a fixed number of rows rather than an unbounded scan.
    /// </summary>
    protected virtual int MaxCandidatesToScan => FeedConsts.MaxItems * 10;

    protected IContentRepository ContentRepository { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    protected NoIndexRecognizer NoIndexRecognizer { get; }

    protected ContentSummaryResolver SummaryResolver { get; }

    public FeedSource(
        IContentRepository contentRepository,
        SiteUrlBuilder urlBuilder,
        NoIndexRecognizer noIndexRecognizer,
        ContentSummaryResolver summaryResolver)
    {
        ContentRepository = contentRepository;
        UrlBuilder = urlBuilder;
        NoIndexRecognizer = noIndexRecognizer;
        SummaryResolver = summaryResolver;
    }

    public virtual async Task<List<FeedItem>> GetItemsAsync(
        Page page,
        SiteUrlContext context,
        string cultureName,
        int maxItems = FeedConsts.MaxItems,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = CultureNameNormalizer.Normalize(cultureName);

        var seoField = await NoIndexRecognizer.FindFieldAsync(cancellationToken);
        var lookup = await SummaryResolver.CreateLookupAsync(page.Id, cancellationToken);

        var items = new List<FeedItem>();
        var asOf = Clock.Now;
        var skipCount = 0;

        // Paged rather than a single fixed-size fetch: noindex lives in the value bag, not a column, so it
        // cannot be pushed into the query. A one-shot "maxItems * N" fetch can silently come back with
        // fewer than maxItems whenever more than that fraction of the window is noindexed, even though
        // enough indexable content exists further back - paging keeps asking until it has enough, the page
        // genuinely runs out of content, or the scan cap above is hit.
        while (items.Count < maxItems && skipCount < MaxCandidatesToScan)
        {
            var candidates = await ContentRepository.GetListAsync(
                pageId: page.Id,
                cultureName: normalizedCulture,
                status: ContentStatus.Published,
                publishedBefore: asOf,
                maxResultCount: maxItems,
                skipCount: skipCount,
                sorting: $"{nameof(Content.PublishTime)} desc",
                cancellationToken: cancellationToken);

            if (candidates.Count == 0)
            {
                break;
            }

            skipCount += candidates.Count;

            foreach (var content in candidates)
            {
                if (items.Count >= maxItems)
                {
                    break;
                }

                // noindex is an author saying "keep this out of discovery". A public feed is discovery, so
                // it is honoured here too rather than being read as a search-engine-only instruction.
                if (NoIndexRecognizer.IsNoIndex(content, seoField))
                {
                    continue;
                }

                var summary = SummaryResolver.Resolve(
                    content, lookup, string.IsNullOrWhiteSpace(content.Slug) ? page.DisplayName : content.Slug);

                items.Add(new FeedItem(
                    UrlBuilder.BuildContentUrl(context, page, content),
                    summary.Title,
                    summary.Summary,
                    content.PublishTime,
                    ContentModificationTime.Of(content)));
            }

            if (candidates.Count < maxItems)
            {
                // A short page means there is nothing further to page through.
                break;
            }
        }

        return items;
    }
}
