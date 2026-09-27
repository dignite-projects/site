/**
 * The value a `Seo` field stores. Mirrors `SeoFieldValue`
 * (`src/Dignite.FlexFields.Site/Dignite/FlexFields/Site/Seo/SeoFieldValue.cs`); the server serializes it with
 * `JsonSerializerDefaults.Web`, so the wire names are camelCase exactly as written here.
 *
 * One field, one composite value - a content type opts into the whole bundle with a single reference
 * rather than pulling in four separate fields.
 */
export interface SeoFieldValue {
  /** Overrides `<title>`/`og:title`; empty falls back to the content's own title field. */
  metaTitle?: string;

  /** Overrides `<meta name="description">`/`og:description`. */
  metaDescription?: string;

  /**
   * Absolute image URL for social sharing. The admin UI fills it with the `url` of a file picked from the
   * `site-images` FileExplorer container; any absolute image URL is equally valid (an MCP client may
   * write one). A FileExplorer image is emitted as `og:image` cropped to 1200x630.
   */
  ogImage?: string;

  /**
   * The one platform-recognized semantic: when true the content is excluded from the sitemap and gets
   * a `noindex` robots meta tag.
   */
  noIndex?: boolean;
}
