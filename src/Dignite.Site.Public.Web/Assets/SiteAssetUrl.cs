namespace Dignite.Site.Public.Assets;

/// <summary>
/// The address a static file of the theme is served at: <c>/site-assets/{path}?v={hash}</c>, where <c>{path}</c> is the
/// file's full path in the theme (<c>assets/site.css</c>) and <c>{hash}</c> the SHA-256 of its content. What
/// <c>asset_url</c> writes and <see cref="SiteAssetController"/> answers.
/// </summary>
public static class SiteAssetUrl
{
    /// <summary>The first segment of every static file's address.</summary>
    public const string RoutePrefix = "site-assets";

    /// <summary>
    /// The query parameter carrying the content hash. It changes the address whenever the content changes, so a browser
    /// never keeps an old file; the response is cached for good only when it matches the content served.
    /// </summary>
    public const string VersionQueryKey = "v";
}
