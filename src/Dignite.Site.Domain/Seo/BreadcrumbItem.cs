namespace Dignite.Site.Seo;

/// <summary>One step of <see cref="HeadMetadata.Breadcrumb"/>.</summary>
public class BreadcrumbItem
{
    public BreadcrumbItem(string name, string path, string url, string pageName, string? slug = null)
    {
        Name = name;
        Path = path;
        Url = url;
        PageName = pageName;
        Slug = slug;
    }

    /// <summary>
    /// The title of the page or content in the language being viewed, by the rule its own head uses: the
    /// page's own content's title, else the page's display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// <see cref="Url"/> as a site-relative path - culture prefix included, the site's base path not - for a
    /// link within whichever host the visitor is on, as <see cref="HreflangAlternate.Path"/>.
    /// </summary>
    public string Path { get; }

    /// <summary>The absolute URL on the site's primary domain, for structured data.</summary>
    public string Url { get; }

    /// <summary>
    /// The name of the page the step stands for - for a content's step, the page it is under - so that a template can
    /// label a step with a text of its own (a short menu name, say) instead of <see cref="Name"/>.
    /// </summary>
    public string PageName { get; }

    /// <summary>The content's slug on a content's step; null on a page's.</summary>
    public string? Slug { get; }
}
