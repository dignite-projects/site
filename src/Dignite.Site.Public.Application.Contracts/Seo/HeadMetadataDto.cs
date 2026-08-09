using System.Collections.Generic;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Everything a renderer needs to put in one resolved route's <c>&lt;head&gt;</c> (总体设计 §5.3, §5.5,
/// §5.9 - GitHub issues #13, #16, #17). JSON-LD is deliberately not part of this contract (总体设计 §5.4
/// decision) - a renderer that wants structured data builds it itself from the same content and field
/// data, rather than being bound to a schema.org mapping this backend decided on its behalf.
/// <para>
/// Plain strings and lists only, the same minimal-custom-shape principle <c>SiteDocument</c> already
/// follows for sitemap/robots/feed - no <c>SeoTags</c> type appears here, since this project never
/// references <c>Dignite.Site.Domain</c> or that package.
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
}
