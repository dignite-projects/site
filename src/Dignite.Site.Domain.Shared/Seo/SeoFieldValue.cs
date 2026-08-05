namespace Dignite.Site.Seo;

/// <summary>
/// The value shape stored under the <see cref="SeoFieldNames.FieldName"/> field: everything a
/// <c>ContentType</c> gets by pulling in the platform's SEO field (总体设计 §5.3).
/// <para>
/// One field, one composite value - not four separate fields - so a content type opts into the whole
/// bundle with a single reference, and the value round-trips through <c>Content.FlexFields</c> like any
/// other <c>IHasFlexFields</c> value (a live object in memory, a <c>JsonElement</c> after a database round
/// trip; <c>GetField&lt;TField&gt;</c> already unwraps both, see <c>FlexFieldDictionaryExtensions</c>).
/// </para>
/// <para>
/// Deliberately kept to the core set - title, description, image, noindex - that both surveyed prior art
/// (Craft CMS's SEOmatic and Ether Creative's SEO field type) treat as their base layer, before their own
/// optional extras (OG/Twitter-specific overrides, a fuller robots directive set, canonical overrides,
/// keyword scoring). Nothing here needs a migration to grow later - it is one JSON-backed value - so the
/// smaller shape is a scope choice, not a technical constraint.
/// </para>
/// </summary>
public class SeoFieldValue
{
    /// <summary>Overrides <c>&lt;title&gt;</c>/<c>og:title</c>; empty falls back to the content's own title field.</summary>
    public string? MetaTitle { get; set; }

    /// <summary>Overrides <c>&lt;meta name="description"&gt;</c>/<c>og:description</c>.</summary>
    public string? MetaDescription { get; set; }

    /// <summary>
    /// Absolute image URL for social sharing. A plain string until a media/file field type exists in this
    /// solution - see <c>SeoFieldType</c>'s remarks.
    /// </summary>
    public string? OgImage { get; set; }

    /// <summary>
    /// The one platform-recognized semantic (总体设计 §5.3): when true, the platform excludes this content
    /// from the sitemap and emits a <c>noindex</c> robots meta tag. Recognition lives in
    /// <c>NoIndexRecognizer</c>, the single place both consumers call.
    /// </summary>
    public bool NoIndex { get; set; }
}
