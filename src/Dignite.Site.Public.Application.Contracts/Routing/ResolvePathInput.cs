using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Public.Routing;

public class ResolvePathInput
{
    /// <summary>
    /// The raw request path, exactly as received - e.g. <c>/blog/my-trip</c>, or culture-prefixed,
    /// <c>/zh-Hans/blog/my-trip</c>. Any served, non-default culture's prefix is stripped server-side;
    /// the caller must not pre-strip it. <see cref="RouteMatchDto.CultureName"/> reports which culture
    /// the match was actually resolved against.
    /// </summary>
    [Required]
    public string Path { get; set; } = default!;
}
