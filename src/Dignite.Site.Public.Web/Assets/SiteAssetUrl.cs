namespace Dignite.Site.Public.Assets;

/// <summary>
/// The address a published <c>assets/...</c> template is served at: <c>/site-assets/{path}?v={id}</c>, where
/// <c>{path}</c> is its name after <c>assets/</c> and <c>{id}</c> the id of its published version, as 32 hex digits. What
/// <c>asset_url</c> writes and <see cref="SiteAssetController"/> answers.
/// </summary>
public static class SiteAssetUrl
{
    /// <summary>The first segment of every asset's address.</summary>
    public const string RoutePrefix = "site-assets";

    /// <summary>
    /// The query parameter carrying the id of the published version. It changes the address on every publish, so a
    /// browser never keeps the old file; the response is cached for good only when it matches the version served.
    /// An id and not a number, because numbers restart when a template is deleted and created again.
    /// </summary>
    public const string VersionQueryKey = "v";
}
