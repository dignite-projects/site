using System;

namespace Dignite.Site.Seo;

/// <summary>Sizes and paths for the per-page, per-language feeds (总体设计 §5.9, GitHub issue #31).</summary>
public static class FeedConsts
{
    /// <summary>
    /// Items in one feed. A feed is a "what changed recently" document, not an archive - that is what the
    /// sitemap is for - so this is deliberately small.
    /// </summary>
    public const int MaxItems = 20;

    /// <summary>
    /// How far a summary derived from a content field is truncated. Only applies to the fallback path; a
    /// value that came from the SEO field's <c>MetaDescription</c> is emitted whole, since an author wrote
    /// it at that length on purpose.
    /// </summary>
    public const int MaxSummaryLength = 500;

    public const string RssFileName = "feed.xml";
    public const string AtomFileName = "atom.xml";
    public const string JsonFileName = "feed.json";

    public const string RssContentType = "application/rss+xml";
    public const string AtomContentType = "application/atom+xml";
    public const string JsonContentType = "application/feed+json";

    /// <summary>The <c>version</c> every generated JSON Feed declares.</summary>
    public const string JsonFeedVersion = "https://jsonfeed.org/version/1.1";

    public static string GetFileName(SiteFeedFormat format)
    {
        return format switch
        {
            SiteFeedFormat.Rss => RssFileName,
            SiteFeedFormat.Atom => AtomFileName,
            SiteFeedFormat.Json => JsonFileName,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public static string GetContentType(SiteFeedFormat format)
    {
        return format switch
        {
            SiteFeedFormat.Rss => RssContentType,
            SiteFeedFormat.Atom => AtomContentType,
            SiteFeedFormat.Json => JsonContentType,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    /// <summary>
    /// Takes a feed request path apart: <c>zh-Hans/about/feed.xml</c> is the RSS feed of whatever
    /// <c>zh-Hans/about</c> addresses.
    /// <para>
    /// The single definition of "this path is a feed address", used both by the route constraint that
    /// decides whether the feed endpoint is even reached and by the service that then serves it. Two
    /// copies of this rule would mean a path one accepts and the other rejects.
    /// </para>
    /// </summary>
    /// <param name="remainingPath">
    /// Everything before the file name, stripped of surrounding slashes - still carrying any language
    /// prefix, which only a <c>SiteUrlContext</c> can interpret.
    /// </param>
    public static bool TryParsePath(string? feedPath, out SiteFeedFormat format, out string remainingPath)
    {
        format = SiteFeedFormat.Rss;
        remainingPath = string.Empty;

        var trimmed = (feedPath ?? string.Empty).Trim('/');
        var lastSlash = trimmed.LastIndexOf('/');

        if (!TryParseFileName(lastSlash < 0 ? trimmed : trimmed[(lastSlash + 1)..], out format))
        {
            return false;
        }

        remainingPath = lastSlash < 0 ? string.Empty : trimmed[..lastSlash];
        return true;
    }

    /// <summary>
    /// Which format a request for this file name is asking for. Case-insensitive, since a URL's last
    /// segment is the one part of a feed address people retype by hand.
    /// </summary>
    public static bool TryParseFileName(string? fileName, out SiteFeedFormat format)
    {
        format = SiteFeedFormat.Rss;

        if (string.Equals(fileName, RssFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(fileName, AtomFileName, StringComparison.OrdinalIgnoreCase))
        {
            format = SiteFeedFormat.Atom;
            return true;
        }

        if (string.Equals(fileName, JsonFileName, StringComparison.OrdinalIgnoreCase))
        {
            format = SiteFeedFormat.Json;
            return true;
        }

        return false;
    }
}
