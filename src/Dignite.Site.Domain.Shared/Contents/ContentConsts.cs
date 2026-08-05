namespace Dignite.Site.Contents;

/// <summary>
/// Column lengths for <c>Content</c>.
/// </summary>
public static class ContentConsts
{
    /// <summary>
    /// Default value: 256. Empty for a page that carries a single content (a home or "about" page) -
    /// the URL is then the page route alone.
    /// </summary>
    public static int MaxSlugLength { get; set; } = 256;

    /// <summary>
    /// Default value: 32. A <c>CultureInfo.Name</c>-form BCP 47 tag (<c>en</c>, <c>zh-Hans</c>,
    /// <c>en-GB</c>). Long enough for the extended tags .NET recognizes.
    /// </summary>
    public static int MaxCultureNameLength { get; set; } = 32;
}
