using System;
using System.Collections.Generic;

namespace Dignite.Site.Seo;

/// <summary>
/// Everything a renderer needs to put in one resolved route's <c>&lt;head&gt;</c> (总体设计 §5.3, §5.5,
/// §5.9 - GitHub issues #13, #16, #17).
/// <para>
/// Pure data: no third-party rendering type appears anywhere in this class or the ones it aggregates, so
/// it is equally usable by the Tier 0 renderer (#21) calling in-process and by any future out-of-process
/// one over HTTP - <see cref="HeadMetadataBuilder"/>'s whole reason to exist.
/// </para>
/// </summary>
public class HeadMetadata
{
    public HeadMetadata(
        string title,
        string baseTitle,
        string? description,
        string? ogImageUrl,
        string canonicalUrl,
        string cultureName,
        bool noIndex,
        IReadOnlyList<HreflangAlternate> hreflangAlternates,
        string? xDefaultUrl)
    {
        Title = title;
        BaseTitle = baseTitle;
        Description = description;
        OgImageUrl = ogImageUrl;
        CanonicalUrl = canonicalUrl;
        CultureName = cultureName;
        NoIndex = noIndex;
        HreflangAlternates = hreflangAlternates;
        XDefaultUrl = xDefaultUrl;
    }

    /// <summary>Never blank - falls back through the same chain <c>ContentSummaryResolver</c> uses for a feed item.</summary>
    public string Title { get; }

    /// <summary>
    /// <see cref="Title"/> before a declared filtered view's filter values were appended to it - the page's
    /// own title, e.g. "Blog" where <see cref="Title"/> is "Blog - engineering". Equal to <see cref="Title"/>
    /// whenever nothing was appended, so a template can tell the two cases apart and swap in a localized
    /// name for the filter value, which this API-side builder has no way to translate.
    /// </summary>
    public string BaseTitle { get; }

    /// <summary>
    /// The language this route resolved in, normalized BCP 47 - the matched content's own
    /// <c>CultureName</c> when there is one, otherwise the requested language.
    /// </summary>
    public string CultureName { get; }

    public string? Description { get; }

    /// <summary>Null omits <c>og:image</c> - there is no platform-wide default image to fall back to.</summary>
    public string? OgImageUrl { get; }

    /// <summary>Derived from the route, never read from a field (总体设计 §5.3(2)).</summary>
    public string CanonicalUrl { get; }

    /// <summary>
    /// True when the content's own SEO field says so, or when this metadata was built for a preview
    /// (<c>includeUnpublished: true</c>) - <c>SiteRouteResolver.ResolveAsync</c>'s own documented
    /// obligation, discharged here (GitHub issue #16).
    /// </summary>
    public bool NoIndex { get; }

    /// <summary>Reciprocal, self-referencing, absolute (总体设计 §5.5). Includes the current language.</summary>
    public IReadOnlyList<HreflangAlternate> HreflangAlternates { get; }

    /// <summary>
    /// The default-language member of <see cref="HreflangAlternates"/> - this same page, never another one.
    /// Null when there is only one alternate, or none of them is in the default language.
    /// </summary>
    public string? XDefaultUrl { get; }

    /// <summary>
    /// Whether <see cref="OgImageUrl"/> is the site's default share image (<c>Site.Seo.DefaultShareImage</c>)
    /// rather than the content's own: a template that has an image of its own for the page, a post's cover say,
    /// puts it in place of the default but never in place of an image the author chose.
    /// </summary>
    public bool IsDefaultOgImage { get; init; }

    /// <summary>
    /// The size <see cref="OgImageUrl"/> is served at, for <c>og:image:width</c>/<c>og:image:height</c>: an image
    /// of the file library is cropped to <see cref="OpenGraphConsts"/>' size. Null for an external image, whose
    /// size is unknown.
    /// </summary>
    public int? OgImageWidth { get; init; }

    /// <inheritdoc cref="OgImageWidth"/>
    public int? OgImageHeight { get; init; }

    /// <summary>The site's name, <c>Site.Branding.AppName</c>, for <c>og:site_name</c>; null when it is not set.</summary>
    public string? SiteName { get; init; }

    /// <summary>
    /// The site's primary origin (<see cref="SiteUrlContext.BaseUrl"/>), which every absolute URL here is built on,
    /// so that a renderer makes the addresses it writes itself - in structured data, say - absolute the same way.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// The way from the home page to this route: the page's ancestors (<c>Page.ParentId</c>), the home page first,
    /// then the page, then the content when the route is one. Derived from the page tree like the canonical URL
    /// (总体设计 §5.3(2)), for a visible breadcrumb and a <c>BreadcrumbList</c> alike.
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Breadcrumb { get; init; } = Array.Empty<BreadcrumbItem>();
}
