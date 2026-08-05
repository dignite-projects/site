using System.Collections.Generic;

namespace Dignite.Site.Seo;

/// <summary>
/// Everything a renderer needs to put in one resolved route's <c>&lt;head&gt;</c> (总体设计 §5.3, §5.4,
/// §5.5, §5.9 - GitHub issues #13, #16, #17, #20).
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
        string? description,
        string? ogImageUrl,
        string canonicalUrl,
        string cultureName,
        bool noIndex,
        IReadOnlyList<HreflangAlternate> hreflangAlternates,
        string? xDefaultUrl,
        JsonLdSiteData siteData,
        IReadOnlyList<JsonLdBreadcrumbItem> breadcrumb,
        JsonLdContentData? contentData)
    {
        Title = title;
        Description = description;
        OgImageUrl = ogImageUrl;
        CanonicalUrl = canonicalUrl;
        CultureName = cultureName;
        NoIndex = noIndex;
        HreflangAlternates = hreflangAlternates;
        XDefaultUrl = xDefaultUrl;
        SiteData = siteData;
        Breadcrumb = breadcrumb;
        ContentData = contentData;
    }

    /// <summary>Never blank - falls back through the same chain <c>ContentSummaryResolver</c> uses for a feed item.</summary>
    public string Title { get; }

    /// <summary>
    /// The language this route resolved in, normalized BCP 47 - the matched content's own
    /// <c>CultureName</c> when there is one, otherwise the requested language. What JSON-LD's
    /// <c>inLanguage</c> is built from.
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

    /// <summary>The home page's URL in the default language, or null if no page currently holds <c>IsHomePage</c>.</summary>
    public string? XDefaultUrl { get; }

    public JsonLdSiteData SiteData { get; }

    /// <summary>Home first, this route last. A single entry when this route is the home page itself.</summary>
    public IReadOnlyList<JsonLdBreadcrumbItem> Breadcrumb { get; }

    /// <summary>Null when the route matched a bare page (no content), or its content type has no schema.org mapping.</summary>
    public JsonLdContentData? ContentData { get; }
}
