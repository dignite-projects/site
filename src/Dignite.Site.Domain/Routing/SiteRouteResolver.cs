using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Routing;

/// <summary>
/// Turns a request path into a page and, if the path carries one, a content (总体设计 §3.4).
/// <para>
/// The routing table is data, not compiled routes: publishing a content adds a URL, and nothing has to
/// be rebuilt or restarted for it to resolve. This is also the single source that sitemap, canonical and
/// hreflang are derived from, which is why the page collection has to exist server-side even when a
/// front end does its own routing (§3.1).
/// </para>
/// <para>
/// Tenant resolution happens before any of this - the host name has already selected the tenant, and
/// ABP's data filter keeps every query below inside it.
/// </para>
/// </summary>
public class SiteRouteResolver : DomainService
{
    protected IPageRepository PageRepository { get; }

    protected IContentRepository ContentRepository { get; }

    protected IContentTypeRepository ContentTypeRepository { get; }

    protected RouteMatchCache RequestCache { get; }

    public SiteRouteResolver(
        IPageRepository pageRepository,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        RouteMatchCache requestCache)
    {
        PageRepository = pageRepository;
        ContentRepository = contentRepository;
        ContentTypeRepository = contentTypeRepository;
        RequestCache = requestCache;
    }

    /// <summary>
    /// Resolves <paramref name="path"/> for <paramref name="cultureName"/>.
    /// <para>
    /// Tries the longest prefix of <paramref name="path"/> first - the whole path itself - and, failing
    /// that, one fewer trailing segment at a time, down to the root. At each length, every active page
    /// whose own address is that prefix (总体设计 §3.4 - literal beats derived, then a stable order) is
    /// tried in turn via <see cref="ResolveAgainstPageAsync"/>, not just the first one: more than one page
    /// can legitimately derive the same address (see <c>EfCorePageRepository.RouteExistsAsync</c>'s own
    /// remarks), and only inspecting the highest-priority candidate risks silently shadowing whichever one
    /// actually has the requested content - see <see cref="ResolveAgainstPageAsync"/>'s remarks for what
    /// "didn't pan out, try the next one" means. The first candidate, at the first length, that produces
    /// an actual match wins outright: there is no backtracking to a shorter prefix once some candidate at
    /// this length has matched. A page whose own address happens to sit in the middle of some other page's
    /// deeper template - <c>/blog/abc</c> claimed by its own page, ahead of the derived address
    /// <c>/blog/abc/{publishTime:yyyy-MM}/{slug}</c> would otherwise land on - is exactly why this has to
    /// walk every length rather than only the full path or only each page's own address: the middle of a
    /// request is not always still talking about the page whose template happens to be longest.
    /// </para>
    /// </summary>
    /// <param name="includeUnpublished">
    /// Whether drafts and scheduled contents can match. False for public traffic; true is what a preview
    /// URL passes - and a caller that does so has to force <c>noindex</c> on the response, since an
    /// indexable preview is duplicate content against the real URL (总体设计 §5.3).
    /// </param>
    public virtual async Task<RouteMatch> ResolveAsync(
        string path,
        string cultureName,
        bool includeUnpublished = false,
        CancellationToken cancellationToken = default)
    {
        return await RequestCache.GetOrResolveAsync(
            path,
            cultureName,
            includeUnpublished,
            () => ResolveCoreAsync(path, cultureName, includeUnpublished, cancellationToken));
    }

    protected virtual async Task<RouteMatch> ResolveCoreAsync(
        string path,
        string cultureName,
        bool includeUnpublished,
        CancellationToken cancellationToken)
    {
        if (!CultureNameNormalizer.TryNormalize(cultureName, out var normalizedCulture))
        {
            return RouteMatch.None;
        }

        var normalizedPath = Page.NormalizeRoute(path ?? "/");
        var segments = SplitSegments(normalizedPath);

        // An internal empty segment only comes from a doubled "/" ("/blog//2026-08"). Left alone, a
        // prefix built from it (e.g. "/blog/") would round-trip through Page.NormalizeRoute's own
        // Trim('/') back down to a shorter, clean-looking address ("/blog") that silently matches - the
        // malformed path would then resolve identically to a genuinely clean one. Rejecting it outright
        // here is simpler than trying to make every downstream comparison tolerate it.
        if (Array.IndexOf(segments, "") >= 0)
        {
            return RouteMatch.None;
        }

        // The root is only ever a candidate when the request itself is "/" - shrinking an unrelated,
        // deeper path all the way down to nothing must not land on whatever page happens to sit at "/"
        // (almost always the home page) just because there was nowhere shorter left to try. That would
        // turn every unmatched path into a home page hit instead of a miss.
        var shortestLength = segments.Length == 0 ? 0 : 1;

        // Fetched once for the whole walk, not once per length: every candidate at every length is a
        // lookup within this same active-page list, so there is no reason to round-trip the database once
        // per prefix - this is exactly the "whole routing table" read SitemapEntrySource/LlmsTxtBuilder
        // already do the same way.
        var routablePages = await PageRepository.GetRoutableListAsync(cancellationToken);

        for (var length = segments.Length; length >= shortestLength; length--)
        {
            var prefix = BuildPrefix(segments, length);

            var candidates = routablePages
                .Where(p => PageRoute.GetPath(p.Route) == prefix)
                .OrderBy(p => PageRoute.IsTemplate(p.Route) ? 1 : 0)
                .ThenBy(p => p.Route, StringComparer.Ordinal);

            foreach (var page in candidates)
            {
                var match = await ResolveAgainstPageAsync(
                    page, normalizedPath, normalizedCulture, includeUnpublished, cancellationToken);

                if (match.IsMatch)
                {
                    return match;
                }
            }
        }

        // No candidate, at any length, ever produced a match. The caller consults the redirect table
        // next, and 404s if that misses too.
        return RouteMatch.None;
    }

    /// <summary>
    /// What the rest of <paramref name="normalizedPath"/> means to <paramref name="page"/>, one candidate
    /// among possibly several sharing the same address at this prefix length (总体设计 §3.4) - full detail,
    /// partial filter, or neither, tried in that order (most specific first, same reasoning as
    /// <see cref="ResolveAsync"/>'s own prefix walk one level up).
    /// <para>
    /// A full match whose slug actually resolves to visible content is definitive - <see cref="ResolveAsync"/>
    /// returns it immediately, no other candidate or shorter prefix gets a look. Everything else this
    /// method can return - <see cref="RouteMatch.None"/> from a slug that structurally fit but named no
    /// visible content, included - is <see cref="RouteMatch.IsMatch"/> <c>false</c> or otherwise just this
    /// one candidate's answer, and <see cref="ResolveAsync"/> moves on to the next candidate at the same
    /// length before giving up on it: <c>page.Route</c> matching the shape of the request is not proof
    /// that <c>page</c> is the one that actually owns the requested content when another page's route
    /// could have matched just as well.
    /// </para>
    /// </summary>
    protected virtual async Task<RouteMatch> ResolveAgainstPageAsync(
        Page page,
        string normalizedPath,
        string cultureName,
        bool includeUnpublished,
        CancellationToken cancellationToken)
    {
        if (PageRoute.TryMatchSlug(page.Route, normalizedPath, out var slug))
        {
            var content = await ContentRepository.FindBySlugAsync(page.Id, cultureName, slug, cancellationToken);

            return content != null && IsVisible(content, includeUnpublished)
                ? RouteMatch.ForContent(page, content, await FindContentTypeAsync(content, cancellationToken))
                : RouteMatch.None;
        }

        if (PageRoute.TryMatchPartial(page.Route, normalizedPath, out var filterValues))
        {
            return RouteMatch.ForPage(page, filterValues);
        }

        return await ResolvePageItselfAsync(page, cultureName, includeUnpublished, cancellationToken);
    }

    /// <summary>
    /// A page's own route resolves to the page - unless the page carries a single content with an empty
    /// slug, in which case that content is what the URL means. Also what a request lands on when it
    /// matched some page's own address but the remainder trails off into a shape that page's route cannot
    /// use at all - a literal page in the way of a longer template's own address, say - rather than
    /// backtracking to try a different, shorter-addressed page instead (总体设计 §3.4).
    /// </summary>
    protected virtual async Task<RouteMatch> ResolvePageItselfAsync(
        Page page,
        string cultureName,
        bool includeUnpublished,
        CancellationToken cancellationToken)
    {
        var content = await ContentRepository.FindBySlugAsync(page.Id, cultureName, string.Empty, cancellationToken);

        if (content != null && IsVisible(content, includeUnpublished))
        {
            return RouteMatch.ForContentOfPage(page, content, await FindContentTypeAsync(content, cancellationToken));
        }

        return RouteMatch.ForPage(page);
    }

    /// <summary>The path's segments, e.g. <c>["blog", "abc", "2026-08"]</c> for <c>/blog/abc/2026-08</c>; empty for <c>/</c>.</summary>
    protected virtual string[] SplitSegments(string normalizedPath)
    {
        return normalizedPath == "/" ? Array.Empty<string>() : normalizedPath[1..].Split('/');
    }

    /// <summary>The path formed by the first <paramref name="length"/> of <paramref name="segments"/> - <c>"/"</c> when <paramref name="length"/> is 0.</summary>
    protected virtual string BuildPrefix(string[] segments, int length)
    {
        return length == 0 ? "/" : "/" + string.Join('/', segments[..length]);
    }

    protected virtual async Task<ContentType?> FindContentTypeAsync(Content content, CancellationToken cancellationToken)
    {
        return await ContentTypeRepository.FindAsync(content.ContentTypeId, cancellationToken: cancellationToken);
    }

    protected virtual bool IsVisible(Content content, bool includeUnpublished)
    {
        return includeUnpublished || content.IsPublished(Clock.Now);
    }
}
