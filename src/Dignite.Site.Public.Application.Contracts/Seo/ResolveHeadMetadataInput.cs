using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Public.Seo;

public class ResolveHeadMetadataInput
{
    /// <summary>
    /// The raw request path, exactly as received - e.g. <c>/blog/my-trip</c>, or culture-prefixed,
    /// <c>/zh-Hans/blog/my-trip</c>. Any served, non-default culture's prefix is stripped server-side;
    /// the caller must not pre-strip it.
    /// </summary>
    [Required]
    public string Path { get; set; } = default!;
}
