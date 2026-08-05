namespace Dignite.Site.ContentTypes;

/// <summary>
/// The schema.org type a content type's contents represent, if any (总体设计 §5.4, GitHub issue #20).
/// <para>
/// Deliberately a small, closed set - only the types that still produce a rich result as of 2025/2026.
/// FAQPage and HowTo are excluded on purpose: FAQ rich results were retired in May 2025 and HowTo has
/// produced nothing since 2023, so emitting either would be JSON-LD for AI consumption only, not the SERP
/// treatment the design doc's curated list is chasing.
/// </para>
/// </summary>
public enum SchemaOrgType
{
    /// <summary>No JSON-LD is generated for contents of this type beyond the site-wide nodes.</summary>
    None = 0,

    Article = 1,

    NewsArticle = 2,

    Product = 3
}
