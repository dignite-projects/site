namespace Dignite.Site.Public.Seo;

/// <summary>One step of <see cref="HeadMetadataDto.Breadcrumb"/>.</summary>
public class BreadcrumbItemDto
{
    /// <summary>The title of the page or content, in the language being viewed.</summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// <see cref="Url"/> as a site-relative path - culture prefix included, the site's base path not - for a link
    /// within whichever host the visitor is on, as <see cref="HreflangAlternateDto.Path"/>.
    /// </summary>
    public string Path { get; set; } = default!;

    /// <summary>The absolute URL on the site's primary domain, for structured data.</summary>
    public string Url { get; set; } = default!;

    /// <summary>Whether this step is the route being rendered: the last one.</summary>
    public bool IsCurrent { get; set; }
}
