using System.Collections.Generic;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Everything a renderer needs to put in one resolved route's <c>&lt;head&gt;</c> (总体设计 §5.3, §5.4,
/// §5.5, §5.9 - GitHub issues #13, #16, #17, #20).
/// <para>
/// Plain strings and lists only, the same minimal-custom-shape principle <c>SiteDocument</c> already
/// follows for sitemap/robots/feed - no <c>SeoTags</c> or <c>Schema.NET</c> type appears here, since this
/// project never references <c>Dignite.Site.Domain</c> or those packages.
/// </para>
/// </summary>
public class HeadMetadataDto
{
    public string MetaTitle { get; set; } = default!;

    public string? MetaDescription { get; set; }

    public string CanonicalUrl { get; set; } = default!;

    /// <summary>Null means indexable; otherwise the <c>&lt;meta name="robots"&gt;</c> content, e.g. <c>"noindex"</c>.</summary>
    public string? RobotsContent { get; set; }

    public string? OgImageUrl { get; set; }

    /// <summary>The Open Graph type SeoTags decided on, e.g. <c>"Website"</c>.</summary>
    public string? OgType { get; set; }

    /// <summary>The Twitter card type SeoTags decided on based on whether an image is set, e.g. <c>"SummaryLargeImage"</c>.</summary>
    public string? TwitterCardType { get; set; }

    /// <summary>Reciprocal, self-referencing, absolute (总体设计 §5.5). Includes the current language.</summary>
    public List<HreflangAlternateDto> HreflangAlternates { get; set; } = new();

    public string? XDefaultUrl { get; set; }

    /// <summary>
    /// The commonly-expected schema.org properties for this content's mapped type that came back blank or
    /// unmapped - a local structural stand-in for "would this pass a Rich Results check", with no call to
    /// any external service. Empty when the route has no content-level schema.org mapping at all. Purely
    /// informational: nothing in this codebase blocks a publish on it.
    /// </summary>
    public List<string> MissingSchemaProperties { get; set; } = new();

    /// <summary>
    /// Each entry is a complete, self-contained JSON-LD document (its own <c>@context</c>/<c>@type</c>) -
    /// one <c>&lt;script type="application/ld+json"&gt;</c> block per entry. Multiple independent blocks
    /// are valid per Google's own guidance; there is no need to merge them into one <c>@graph</c>.
    /// </summary>
    public List<string> JsonLdDocuments { get; set; } = new();
}
