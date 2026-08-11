using System;
using System.Collections.Generic;
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
    /// whose own address is that prefix (总体设计 §3.4) is a candidate, and more than one page legitimately
    /// can derive the same address (see <c>EfCorePageRepository.RouteExistsAsync</c>'s own remarks) - so
    /// each length is tried in two passes rather than picking one candidate by a fixed priority up front.
    /// The first pass offers every candidate at this length to <see cref="ResolveAgainstPageAsync"/>, which
    /// only ever answers when the request's remainder structurally fits that candidate's route (a full slug
    /// or a partial filter): whichever candidate does so first wins outright, deepest information taking
    /// priority over tie-break order. Only when nothing at this length structurally accounted for the
    /// remainder does the second pass run, offering the same candidates - in a stable, literal-before-
    /// template order - to <see cref="ResolvePageItselfAsync"/>'s unconditional "this page, as itself"
    /// fallback. A page whose own address happens to sit in the middle of some other page's deeper template
    /// - <c>/blog/abc</c> claimed by its own page, ahead of the derived address
    /// <c>/blog/abc/{publishTime:yyyy-MM}/{slug}</c> would otherwise land on - only wins there when the
    /// deeper template's own candidacy, tried first in the same pass, could not resolve this specific
    /// request either; this is exactly why this has to walk every length <i>and</i> run two passes at each
    /// one, rather than only the full path or only each page's own address: the middle of a request is not
    /// always still talking about the page whose template happens to be longest, but it is not automatically
    /// talking about a shorter literal page in the way either. Whichever length this settles on, once any
    /// candidate there has matched (in either pass), there is no backtracking to a shorter prefix.
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
                .ThenBy(p => p.Route, StringComparer.Ordinal)
                .ToList();

            // A candidate with nothing structural to say about the remainder - neither TryMatchSlug nor
            // TryMatchPartial applied at all, signalled by null - is the only kind still eligible for the
            // page-itself fallback below. One that did structurally apply, even to a definitive miss (a
            // slug naming no visible content), has already given its final answer for this request and
            // must not get a second, different one from that fallback - see ResolveAgainstPageAsync's
            // remarks for why a miss there is not the same as having nothing to say.
            var eligibleForFallback = new List<Page>();

            foreach (var page in candidates)
            {
                var match = await ResolveAgainstPageAsync(
                    page, normalizedPath, normalizedCulture, includeUnpublished, cancellationToken);

                if (match == null)
                {
                    eligibleForFallback.Add(page);
                    continue;
                }

                if (match.IsMatch)
                {
                    return match;
                }
            }

            // Nothing at this length produced a visible structural answer - only now does the tie-break's
            // loser get a claim, in the same literal-before-template order as above, and only among
            // candidates that never had a structural opinion to begin with.
            foreach (var page in eligibleForFallback)
            {
                var match = await ResolvePageItselfAsync(page, normalizedCulture, includeUnpublished, cancellationToken);

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
    /// What the rest of <paramref name="normalizedPath"/> structurally means to <paramref name="page"/> -
    /// full detail or partial filter, tried in that order (most specific first, same reasoning as
    /// <see cref="ResolveCoreAsync"/>'s own prefix walk one level up) - or <see langword="null"/> when
    /// <paramref name="page"/>'s route has no structural opinion about the remainder at all (neither shape
    /// fits). That distinction is the whole point of the nullable return: a slug that structurally fit but
    /// named no visible content is this candidate's final, definitive answer for this request - a
    /// different request might still legitimately land on this same page's own address, but not this one -
    /// so it comes back as non-null <see cref="RouteMatch.None"/>, not <see langword="null"/>, and
    /// <see cref="ResolveCoreAsync"/> must not let it fall through to <see cref="ResolvePageItselfAsync"/>'s
    /// unconditional fallback; a page with nothing to say has that fallback still open to it. Getting this
    /// wrong once quietly served an unpublished draft's list page instead of a 404 for its own now-invisible
    /// detail URL - exactly the regression this distinction exists to prevent.
    /// <para>
    /// A full match whose slug actually resolves to visible content is definitive the other way too -
    /// <see cref="ResolveCoreAsync"/> returns it immediately, no other candidate or shorter prefix gets a
    /// look. Everything else non-null this method can return is <see cref="RouteMatch.IsMatch"/>
    /// <c>false</c>, and <see cref="ResolveCoreAsync"/> moves on to the next candidate at the same length
    /// before giving up on it: <c>page.Route</c> matching the shape of the request is not proof that
    /// <c>page</c> is the one that actually owns the requested content when another page's route could have
    /// matched just as well.
    /// </para>
    /// </summary>
    protected virtual async Task<RouteMatch?> ResolveAgainstPageAsync(
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

        return null;
    }

    /// <summary>
    /// A page's own route resolves to the page - unless the page carries a single content with an empty
    /// slug, in which case that content is what the URL means. Also the last-resort answer for a request
    /// that matched some page's own address but whose remainder trails off into a shape this page's route
    /// cannot use at all - tried only in <see cref="ResolveCoreAsync"/>'s second pass over a length, and
    /// only for a candidate whose <see cref="ResolveAgainstPageAsync"/> call came back <see langword="null"/>
    /// (总体设计 §3.4): one that came back non-null - even <see cref="RouteMatch.None"/> - had a structural
    /// opinion and already gave its final answer, so it is excluded from this fallback rather than getting
    /// a second, different one here. A literal page sharing a longer template's own address therefore
    /// shadows it only when the template itself had nothing structural to say about the specific request
    /// either - not unconditionally, the way a single-pass walk would have it, and not merely because the
    /// template's own structural attempt happened to miss.
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
