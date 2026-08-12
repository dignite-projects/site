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
    /// each length runs up to three tiers rather than picking one candidate by a fixed priority up front.
    /// </para>
    /// <para>
    /// Tier 1 offers every candidate at this length, in a stable literal-before-template order, to
    /// <see cref="ResolveAgainstPageAsync"/>'s structural attempt - a full slug
    /// (<see cref="PageRoute.TryMatchSlug"/>) or every one of a non-slug route's own placeholders filled
    /// with none dropped (<see cref="PageRoute.TryMatchExact"/>): whichever candidate structurally fits
    /// first wins outright, deepest information taking priority over tie-break order.
    /// </para>
    /// <para>
    /// Tier 3 - every placeholder but one dropped from the end, e.g. every one before <c>{slug}</c> but
    /// not slug itself (<see cref="PageRoute.TryMatchPartial"/>) - is tried inline as part of the very same
    /// <see cref="ResolveAgainstPageAsync"/> call, so for the one candidate it can ever apply to, it is
    /// tried <i>before</i> tier 2 below despite the numbering. It only ever runs when that candidate is the
    /// <i>sole</i> one at this length: <c>/blog/abc/{publishTime:yyyy-MM}/{slug}</c> answering
    /// <c>/blog/abc/2026-08</c> by dropping slug is this tier, and it stops being available the moment an
    /// admin also creates a page whose own address is <c>/blog/abc</c> or
    /// <c>/blog/abc/{publishTime:yyyy-MM}</c> - even before knowing whether that other page can actually
    /// answer this specific request either. A deep template's own placeholders are not licence to guess at
    /// an address another, more specific page might already own (总体设计 §3.4).
    /// </para>
    /// <para>
    /// Tier 2 - <see cref="ResolvePageItselfAsync"/>'s unconditional "this page, as itself" fallback - only
    /// ever runs for a candidate whose <see cref="ResolveAgainstPageAsync"/> call came back
    /// <see langword="null"/>: no structural opinion in tier 1, and either not eligible for tier 3 (another
    /// candidate shares this address) or tier 3 found nothing to explain the remainder with either. A page
    /// whose own address happens to sit in the middle of some other page's deeper template - <c>/blog/abc</c>
    /// claimed by its own page, ahead of the derived address <c>/blog/abc/{publishTime:yyyy-MM}/{slug}</c>
    /// would otherwise land on - wins here once the deeper template has nothing left to offer, exactly like
    /// it always could; what changed is that an unrelated template guessing at this address via truncation
    /// is off the table first. A template candidate that reaches here with request segments beyond its own
    /// bare address still unaccounted for is deliberately <i>not</i> eligible either, even though its own
    /// <see cref="ResolveAgainstPageAsync"/> call came back <see langword="null"/> the same way a genuinely
    /// bare route's does - "page itself" would silently discard whatever those segments were, exactly the
    /// confusion this whole design exists to prevent. A literal route with no placeholder at all is the
    /// only kind that keeps the unconditional eligibility this fallback has always had, because it never
    /// had any other mechanism to explain extra segments with in the first place.
    /// </para>
    /// <para>
    /// Whichever length this settles on, once any candidate there has matched (in any tier), there is no
    /// backtracking to a shorter prefix.
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

            // Tier 3 (ResolveAgainstPageAsync's own truncated-match attempt) is only ever offered to a
            // candidate that is the only one sharing this address - see ResolveAsync's remarks.
            var isOnlyCandidateAtThisLength = candidates.Count == 1;

            // A candidate with nothing structural to say about the remainder - neither tier 1 nor tier 3
            // applied at all, signalled by null - is the only kind still eligible for the page-itself
            // fallback below. One that did structurally apply, even to a definitive miss (a slug naming no
            // visible content), has already given its final answer for this request and must not get a
            // second, different one from that fallback - see ResolveAgainstPageAsync's remarks for why a
            // miss there is not the same as having nothing to say.
            var eligibleForFallback = new List<Page>();

            foreach (var page in candidates)
            {
                var match = await ResolveAgainstPageAsync(
                    page, normalizedPath, normalizedCulture, includeUnpublished, isOnlyCandidateAtThisLength, cancellationToken);

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
    /// What the rest of <paramref name="normalizedPath"/> structurally means to <paramref name="page"/>,
    /// tried in up to three tiers (总体设计 §3.4, and see <see cref="ResolveAsync"/>'s own remarks for how
    /// they interact with the rest of a resolve) - or <see langword="null"/> when none of them has
    /// anything to say about the remainder at all.
    /// <para>
    /// Tier 1: a full slug when <paramref name="page"/>'s route ends in <c>{slug}</c>/<c>{slug?}</c>
    /// (<see cref="PageRoute.TryMatchSlug"/>), or every one of its placeholders filled with none dropped
    /// when it does not (<see cref="PageRoute.TryMatchExact"/>). A slug that structurally fit but named no
    /// visible content is this candidate's final, definitive answer for this request - a different request
    /// might still legitimately land on this same page's own address, but not this one - so it comes back
    /// as non-null <see cref="RouteMatch.None"/>, not <see langword="null"/>, and must not fall through to
    /// tier 3 or <see cref="ResolvePageItselfAsync"/>'s tier-2 fallback; a page with nothing to say has
    /// both still open to it. Getting this wrong once quietly served an unpublished draft's list page
    /// instead of a 404 for its own now-invisible detail URL - exactly the regression this distinction
    /// exists to prevent. A full match whose slug actually resolves to visible content is definitive the
    /// other way too - <see cref="ResolveCoreAsync"/> returns it immediately, no other candidate or shorter
    /// prefix gets a look.
    /// </para>
    /// <para>
    /// Tier 3, tried immediately after tier 1 within this same call - not a separate pass, which is why
    /// the effective priority for the one candidate it can ever apply to is 1 then 3 then 2, despite the
    /// numbering: every placeholder but one dropped from the end (<see cref="PageRoute.TryMatchPartial"/>),
    /// only when <paramref name="isOnlyCandidateAtThisLength"/>. A route that needs more of the path than
    /// it was given is not licence to guess at an address another, more specific page at the very same
    /// prefix might already own - once a second candidate exists there, this guess is withheld from both,
    /// regardless of whether the other candidate can actually answer the request or not.
    /// </para>
    /// <para>
    /// Everything non-null this method can return other than a genuine content match is
    /// <see cref="RouteMatch.IsMatch"/> <c>false</c>, and <see cref="ResolveCoreAsync"/> moves on to the
    /// next candidate at the same length before giving up on it - <c>page.Route</c> matching the shape of
    /// the request is not proof that <c>page</c> is the one that actually owns it when another page's
    /// route could have matched just as well. That now includes a template whose own placeholders leave
    /// part of the request unaccounted for: tier 2's "page itself" would silently discard whatever those
    /// leftover segments were, so a route that <see cref="PageRoute.IsTemplate"/> reports true for is only
    /// left eligible for it when the request is exactly its own bare address with nothing left over
    /// (<c>normalizedPath == page.GetPath()</c>) - the same condition <see cref="ResolvePageItselfAsync"/>
    /// has always implicitly answered for a route with no placeholder at all, which is the only kind still
    /// unconditionally eligible here, because it never had any mechanism to explain extra segments with in
    /// the first place.
    /// </para>
    /// </summary>
    protected virtual async Task<RouteMatch?> ResolveAgainstPageAsync(
        Page page,
        string normalizedPath,
        string cultureName,
        bool includeUnpublished,
        bool isOnlyCandidateAtThisLength,
        CancellationToken cancellationToken)
    {
        if (PageRoute.HasSlug(page.Route))
        {
            if (PageRoute.TryMatchSlug(page.Route, normalizedPath, out var slug))
            {
                var content = await ContentRepository.FindBySlugAsync(page.Id, cultureName, slug, cancellationToken);

                return content != null && IsVisible(content, includeUnpublished)
                    ? RouteMatch.ForContent(page, content, await FindContentTypeAsync(content, cancellationToken))
                    : RouteMatch.None;
            }
        }
        else if (PageRoute.TryMatchExact(page.Route, normalizedPath, out var exactValues))
        {
            return RouteMatch.ForPage(page, exactValues);
        }

        if (isOnlyCandidateAtThisLength && PageRoute.TryMatchPartial(page.Route, normalizedPath, out var filterValues))
        {
            return RouteMatch.ForPage(page, filterValues);
        }

        return PageRoute.IsTemplate(page.Route) && normalizedPath != page.GetPath()
            ? RouteMatch.None
            : null;
    }

    /// <summary>
    /// A page's own route resolves to the page - unless the page carries a single content with an empty
    /// slug, in which case that content is what the URL means. Also tier 2's last-resort answer for a
    /// request that matched some page's own address but whose remainder trails off into a shape this
    /// page's route cannot use at all - tried only for a candidate whose <see cref="ResolveAgainstPageAsync"/>
    /// call came back <see langword="null"/> (总体设计 §3.4, and see that method's own remarks for exactly
    /// which candidates that is now that a template with leftover segments is deliberately excluded from
    /// it too): a candidate that came back non-null - even <see cref="RouteMatch.None"/> - already gave its
    /// final answer for this request, structurally or via a tier-3 attempt that was either suppressed or
    /// itself came up empty, so it is excluded from this fallback rather than getting a second, different
    /// one here. A literal page sharing a longer template's own address therefore shadows it only when the
    /// template itself had nothing structural to say about the specific request either - not
    /// unconditionally, the way a single-pass walk would have it, and not merely because the template's own
    /// structural attempt happened to miss.
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
