using Dignite.Site.Themes;

namespace Dignite.Site.Public.Themes;

/// <summary>How the public theme services read a static file of the theme.</summary>
internal static class SiteThemeAssets
{
    /// <summary>The path in its stored form, or null when it cannot name a static file of a theme.</summary>
    public static string? NormalizePath(string? path)
    {
        var normalized = SiteThemePaths.Normalize(path ?? string.Empty);
        return SiteThemePaths.IsValid(normalized) && SiteThemePaths.IsAsset(normalized) ? normalized : null;
    }

    public static SiteThemeAssetDto ToAssetDto(this SiteThemeFile file) =>
        new() { Path = file.Path, ContentHash = file.ContentHash, Body = file.Body };

    public static SiteThemeAssetDto ToAssetDto(this SiteReleaseFileContent content) =>
        new() { Path = content.Path, ContentHash = content.ContentHash, Body = content.Body! };
}
