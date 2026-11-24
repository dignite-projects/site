namespace Dignite.Site.Themes;

/// <summary>How a file of a theme is used.</summary>
public enum SiteThemeUsageKind
{
    /// <summary>A page's <c>Template</c> names it.</summary>
    Page = 0,

    /// <summary>A template <c>include</c>s it.</summary>
    Include = 1,

    /// <summary>A template writes its address with <c>asset_url</c>.</summary>
    AssetUrl = 2,

    /// <summary>A page template's front matter chooses it as its layout.</summary>
    Layout = 3,

    /// <summary>The theme's default layout setting names it.</summary>
    DefaultLayout = 4
}
