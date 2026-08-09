/**
 * The value a `Seo` field stores. Mirrors `SeoFieldValue`
 * (`src/Dignite.Site.Domain.Shared/Seo/SeoFieldValue.cs`); the server serializes it with
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
   * Absolute image URL for social sharing. A plain string until a media/file field type exists in this
   * solution - see `SeoFieldType`'s remarks on the server.
   */
  ogImage?: string;

  /**
   * The one platform-recognized semantic: when true the content is excluded from the sitemap and gets
   * a `noindex` robots meta tag.
   */
  noIndex?: boolean;
}
