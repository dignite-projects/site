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

    /// <summary>
    /// The name of the page the step stands for - for a content's step, the page it is under - for a renderer that
    /// labels the steps with texts of its own instead of <see cref="Name"/>.
    /// </summary>
    public string PageName { get; set; } = default!;

    /// <summary>The content's slug on a content's step; null on a page's.</summary>
    public string? Slug { get; set; }

    /// <summary>Whether this step is the route being rendered: the last one.</summary>
    public bool IsCurrent { get; set; }
}
