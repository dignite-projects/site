using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Public.Seo;

public class ResolveHeadMetadataInput
{
    /// <summary>The request path, e.g. <c>/blog/my-trip</c>.</summary>
    [Required]
    public string Path { get; set; } = default!;

    [Required]
    public string CultureName { get; set; } = default!;
}
