using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
using JsonFeedNet;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Renders a page's feed in RSS 2.0, Atom 1.0 or JSON Feed 1.1 (总体设计 §5.9, GitHub issue #31).
/// <para>
/// The <c>&lt;link rel="alternate"&gt;</c> autodiscovery tag that points a browser here is deliberately
/// <b>not</b> emitted by this service - that is part of the <c>&lt;head&gt;</c> output work (#13/#16/#17/#20).
/// What this side contributes to that work is <c>SiteUrlBuilder.BuildFeedUrl</c>, so the tag and the
/// endpoint cannot end up disagreeing about the address.
/// </para>
/// </summary>
public class FeedDocumentAppService : PublicAppService, IFeedDocumentAppService
{
    protected IPageRepository PageRepository { get; }

    protected FeedSource FeedSource { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public FeedDocumentAppService(
        IPageRepository pageRepository,
        FeedSource feedSource,
        SiteUrlBuilder urlBuilder)
    {
        PageRepository = pageRepository;
        FeedSource = feedSource;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<SiteDocument?> ResolveAsync(string feedPath)
    {
        if (!FeedConsts.TryParsePath(feedPath, out var format, out var visitorPath))
        {
            return null;
        }

        var context = await UrlBuilder.CreateContextAsync();

        // What remains is the page's address as a visitor sees it - language prefix included - so it is
        // taken apart by the same context that assembles it in BuildFeedUrl.
        var remainder = visitorPath.Length == 0 ? "/" : Page.NormalizeRoute(visitorPath);

        // Both out values are meaningful either way: no prefix means the default language and the path
        // unchanged, which is exactly what an unprefixed URL asks for.
        context.TryStripCulturePrefix(remainder, out var cultureName, out var pageRoute);

        // The context above already resolved this request's settings once - handed straight to the shared
        // overload rather than through the public GetAsync, which would resolve them a second time.
        return await GetAsync(pageRoute, cultureName, format, context);
    }

    public virtual async Task<SiteDocument?> GetAsync(string pageRoute, string cultureName, SiteFeedFormat format)
    {
        return await GetAsync(pageRoute, cultureName, format, await UrlBuilder.CreateContextAsync());
    }

    protected virtual async Task<SiteDocument?> GetAsync(
        string pageRoute, string cultureName, SiteFeedFormat format, SiteUrlContext context)
    {
        if (!CultureNameNormalizer.TryNormalize(cultureName, out var normalizedCulture))
        {
            return null;
        }

        var page = await PageRepository.FindByRouteAsync(Page.NormalizeRoute(pageRoute));
        if (page is not { IsActive: true })
        {
            return null;
        }

        var items = await FeedSource.GetItemsAsync(page, context, normalizedCulture);

        var pageUrl = UrlBuilder.BuildPageUrl(context, page, normalizedCulture);
        var feedUrl = UrlBuilder.BuildFeedUrl(context, page, normalizedCulture, format);

        // An empty feed still needs a date. The page row's own timestamp is a real stored value, which
        // keeps even this corner away from reporting the generation time.
        var lastUpdated = items.Count > 0
            ? items.Max(i => i.LastModified)
            : page.LastModificationTime ?? page.CreationTime;

        var content = format switch
        {
            SiteFeedFormat.Json => BuildJsonFeed(page, normalizedCulture, pageUrl, feedUrl, items),
            _ => BuildSyndicationFeed(page, normalizedCulture, pageUrl, feedUrl, items, lastUpdated, format)
        };

        return new SiteDocument(FeedConsts.GetContentType(format), content, lastUpdated);
    }

    protected virtual string BuildSyndicationFeed(
        Page page,
        string cultureName,
        string pageUrl,
        string feedUrl,
        List<FeedItem> items,
        DateTime lastUpdated,
        SiteFeedFormat format)
    {
        // The third argument becomes the feed's alternate link - the human-readable page this feed is of.
        var feed = new SyndicationFeed(page.DisplayName, page.DisplayName, new Uri(pageUrl))
        {
            Id = feedUrl,
            Language = cultureName,
            LastUpdatedTime = Clock.ToOffset(lastUpdated),
            Items = items.Select(BuildSyndicationItem).ToList()
        };

        feed.Links.Add(SyndicationLink.CreateSelfLink(new Uri(feedUrl)));

        SyndicationFeedFormatter formatter = format == SiteFeedFormat.Atom
            ? new Atom10FeedFormatter(feed)
            // serializeExtensionsAsAtom: false - otherwise every RSS item carries a duplicate set of Atom
            // elements that no reader asked for.
            : new Rss20FeedFormatter(feed, serializeExtensionsAsAtom: false);

        // A plain StringWriter reports UTF-16 as its encoding, and XmlWriter copies that straight into the
        // XML declaration - which would then contradict the UTF-8 bytes the endpoint actually sends.
        using var writer = new Utf8StringWriter();
        using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true });

        formatter.WriteTo(xmlWriter);
        xmlWriter.Flush();

        return writer.ToString();
    }

    protected virtual SyndicationItem BuildSyndicationItem(FeedItem item)
    {
        var syndicationItem = new SyndicationItem
        {
            // The URL doubles as the identity: permanent, unique, and already in hand.
            Id = item.Location,
            Title = new TextSyndicationContent(item.Title),
            PublishDate = Clock.ToOffset(item.PublishTime),
            LastUpdatedTime = Clock.ToOffset(item.LastModified)
        };

        syndicationItem.Links.Add(SyndicationLink.CreateAlternateLink(new Uri(item.Location)));

        if (item.Summary != null)
        {
            // Summary, not Content: RSS 2.0's <description> is written from this property, and Atom
            // accepts an entry carrying <summary> alone.
            syndicationItem.Summary = new TextSyndicationContent(item.Summary);
        }

        return syndicationItem;
    }

    protected virtual string BuildJsonFeed(
        Page page,
        string cultureName,
        string pageUrl,
        string feedUrl,
        List<FeedItem> items)
    {
        var feed = new JsonFeed
        {
            Version = FeedConsts.JsonFeedVersion,
            Title = page.DisplayName,
            HomePageUrl = pageUrl,
            FeedUrl = feedUrl,
            Language = cultureName,
            Items = items.Select(item => new JsonFeedItem
            {
                Id = item.Location,
                Url = item.Location,
                Title = item.Title,
                // JSON Feed requires an item to carry content_text or content_html. With no summary the
                // title is the only text there is, and an item with neither would be invalid.
                ContentText = item.Summary ?? item.Title,
                Summary = item.Summary,
                DatePublished = Clock.ToOffset(item.PublishTime),
                DateModified = Clock.ToOffset(item.LastModified)
            }).ToList()
        };

        return feed.ToString();
    }

    /// <summary>A <see cref="StringWriter"/> that admits to being UTF-8, so the XML declaration says so.</summary>
    protected sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
