using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Seo;

public class SiteSeoSettingsDto
{
    /// <summary>
    /// The share image (<c>og:image</c>) of a page that has none of its own - <c>Site.Seo.DefaultShareImage</c>: a file
    /// library address or an absolute URL. Null when it is not set.
    /// </summary>
    public string? DefaultShareImage { get; set; }
}

public class UpdateSiteSeoSettingsDto
{
    /// <summary>A file library address (<c>/api/site-public/files/...</c>) or an absolute URL; empty clears it.</summary>
    [StringLength(2048)]
    public string? DefaultShareImage { get; set; }
}
