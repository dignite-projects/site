using System;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// What a template adds to the head tags Site derives from the route (<see cref="SiteHeadMetadataHtml"/>): the
/// arguments of <c>site_head</c> and the attributes of <c>&lt;site-head-metadata&gt;</c>.
/// </summary>
public class SiteHeadOptions
{
    /// <summary>
    /// The page's own title for <c>og:title</c> - e.g. an archive titled with its page number. Falls back to the
    /// route's title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>A description used only when the content has none of its own.</summary>
    public string? FallbackDescription { get; set; }

    /// <summary>
    /// A share image for the page - a post's cover, say - as written: a file library address or an absolute URL.
    /// Used when the content's SEO field has none, in place of the site's default share image.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// <c>og:type</c>: <see cref="WebsiteType"/>, the default, or <see cref="ArticleType"/>, which also writes the
    /// content's publish and modification times.
    /// </summary>
    public string? OgType { get; set; }

    /// <summary>When the content was published, for <c>article:published_time</c>.</summary>
    public DateTime? PublishedTime { get; set; }

    /// <summary>When the content was last changed, for <c>article:modified_time</c>.</summary>
    public DateTime? ModifiedTime { get; set; }

    /// <summary>The structured data, already written by <see cref="Seo.SiteJsonLd.Serialize"/>; null writes none.</summary>
    public string? JsonLd { get; set; }

    public const string WebsiteType = "website";

    public const string ArticleType = "article";

    /// <summary>The <c>og:type</c> values a template may give.</summary>
    public static readonly string[] OgTypes = { WebsiteType, ArticleType };
}
