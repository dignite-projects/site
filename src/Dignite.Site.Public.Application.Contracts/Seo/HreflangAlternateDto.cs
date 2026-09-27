namespace Dignite.Site.Public.Seo;

public class HreflangAlternateDto
{
    public string CultureName { get; set; } = default!;

    public string Url { get; set; } = default!;

    /// <summary>
    /// <see cref="Url"/> as a site-relative path - culture prefix included, the site's base path not - for a
    /// renderer linking within whichever host the visitor is on (GitHub issue #75).
    /// </summary>
    public string Path { get; set; } = default!;
}
