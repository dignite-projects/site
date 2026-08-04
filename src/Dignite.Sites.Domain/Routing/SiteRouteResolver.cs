using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.Pages;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace Dignite.Sites.Routing;

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

    protected IClock Clock { get; }

    public SiteRouteResolver(
        IPageRepository pageRepository,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IClock clock)
    {
        PageRepository = pageRepository;
        ContentRepository = contentRepository;
        ContentTypeRepository = contentTypeRepository;
        Clock = clock;
    }

    /// <summary>
    /// Resolves <paramref name="path"/> for <paramref name="cultureName"/>.
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
        if (!CultureNameNormalizer.TryNormalize(cultureName, out var normalizedCulture))
        {
            return RouteMatch.None;
        }

        var normalizedPath = Page.NormalizeRoute(path ?? "/");

        // Step 1: the path is a page's own route. Checked first because it is both the cheapest lookup
        // and the more specific answer - "/blog" is the blog page itself, never a content of "/" whose
        // slug happens to be "blog".
        var exactPage = await PageRepository.FindByRouteAsync(normalizedPath, cancellationToken: cancellationToken);
        if (exactPage is { IsActive: true })
        {
            return await ResolvePageItselfAsync(exactPage, normalizedCulture, includeUnpublished, cancellationToken);
        }

        // Step 2: the path is a content beneath some page. Candidates come back longest-route-first, so
        // "/blog-archive/x" is tested against "/blog-archive" before "/blog" - matching the shorter
        // prefix first would let one page swallow another's URLs.
        foreach (var page in await PageRepository.GetRoutableListAsync(cancellationToken))
        {
            if (!TryGetRelativePath(page.Route, normalizedPath, out var relativePath))
            {
                continue;
            }

            if (!ContentPathPattern.TryExtractSlug(page.ContentPathPattern, relativePath, out var slug))
            {
                continue;
            }

            var content = await ContentRepository.FindBySlugAsync(page.Id, normalizedCulture, slug, cancellationToken);
            if (content == null || !IsVisible(content, includeUnpublished))
            {
                continue;
            }

            return RouteMatch.ForContent(page, content, await FindContentTypeAsync(content, cancellationToken));
        }

        // Step 3: no match. The caller consults the redirect table next, and 404s if that misses too.
        return RouteMatch.None;
    }

    /// <summary>
    /// A page's own route resolves to the page - unless the page carries a single content with an empty
    /// slug, in which case that content is what the URL means.
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

    protected virtual async Task<ContentType?> FindContentTypeAsync(Content content, CancellationToken cancellationToken)
    {
        return await ContentTypeRepository.FindAsync(content.ContentTypeId, cancellationToken: cancellationToken);
    }

    protected virtual bool IsVisible(Content content, bool includeUnpublished)
    {
        return includeUnpublished || content.IsPublished(Clock.Now);
    }

    /// <summary>
    /// The part of <paramref name="path"/> that follows <paramref name="route"/>, or false if the path
    /// is not beneath that route.
    /// <para>
    /// The segment-boundary check is what stops <c>/blog</c> from claiming <c>/blogroll/x</c>: a plain
    /// <c>StartsWith</c> would match, because the prefix does not have to end where a path segment does.
    /// </para>
    /// </summary>
    protected virtual bool TryGetRelativePath(string route, string path, out string relativePath)
    {
        relativePath = string.Empty;

        if (route == "/")
        {
            relativePath = path.TrimStart('/');
            return relativePath.Length > 0;
        }

        if (!path.StartsWith(route, StringComparison.Ordinal) || path.Length <= route.Length)
        {
            return false;
        }

        if (path[route.Length] != '/')
        {
            return false;
        }

        relativePath = path[(route.Length + 1)..];
        return relativePath.Length > 0;
    }
}
