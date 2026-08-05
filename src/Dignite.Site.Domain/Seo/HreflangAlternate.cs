namespace Dignite.Site.Seo;

/// <summary>
/// One language version of a resolved route, as an absolute URL (总体设计 §5.5, GitHub issue #17). A
/// full <see cref="HeadMetadata.HreflangAlternates"/> list is reciprocal and self-referencing by
/// construction: it always includes the language it was built for, since it comes from the same query
/// that found every translation, this one included.
/// </summary>
public class HreflangAlternate
{
    public HreflangAlternate(string cultureName, string url)
    {
        CultureName = cultureName;
        Url = url;
    }

    /// <summary>
    /// Stored in canonical <c>CultureInfo.Name</c> BCP 47 form (总体设计 §2.4), used verbatim as the
    /// <c>hreflang</c> attribute value - no mapping table.
    /// </summary>
    public string CultureName { get; }

    public string Url { get; }
}
