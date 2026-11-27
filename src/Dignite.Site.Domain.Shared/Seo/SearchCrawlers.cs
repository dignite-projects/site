using System.Collections.Generic;

namespace Dignite.Site.Seo;

/// <summary>
/// The search engine crawlers <c>robots.txt</c> still lets into a site closed to indexing
/// (<c>Site.Robots.AllowIndexing</c> off), so that they can read the <c>noindex</c> its pages then carry.
/// <para>
/// <b>Why a closed site lets anyone in.</b> A crawler never fetches a page <c>robots.txt</c> blocks, so it never
/// sees that page's <c>noindex</c> - and it can still list a blocked URL that other sites link to, without a
/// description. Only a crawler allowed to fetch the page can keep it out of its index, or take it out once it is
/// in. This is why WordPress stopped writing <c>Disallow: /</c> for a site that discourages search engines (5.3).
/// </para>
/// <para>
/// <b>Why only these.</b> They are the crawlers whose engines document that they obey <c>noindex</c>. Every other
/// crawler stays behind <c>Disallow: /</c>, the one rule some of them document: Baidu lists <c>nofollow</c> and
/// <c>noarchive</c> among the robots meta values it supports but not <c>noindex</c>, and the AI crawlers
/// (<see cref="AiCrawlers"/>) name <c>robots.txt</c> alone.
/// </para>
/// </summary>
public static class SearchCrawlers
{
    /// <summary>Crawlers of search engines that obey <c>noindex</c>.</summary>
    public static IReadOnlyList<string> ReadingNoIndex { get; } = new[]
    {
        "Googlebot",
        "Bingbot"
    };
}
