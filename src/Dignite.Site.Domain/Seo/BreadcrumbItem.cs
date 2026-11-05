namespace Dignite.Site.Seo;

/// <summary>One step of <see cref="HeadMetadata.Breadcrumb"/>.</summary>
public class BreadcrumbItem
{
    public BreadcrumbItem(string name, string path, string url)
    {
        Name = name;
        Path = path;
        Url = url;
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
}
