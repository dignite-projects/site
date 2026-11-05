using Dignite.Site.Themes;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The fixed names of the site's preview - the "preview" of the administration side's Theme screen. Anything that
/// builds or recognizes a preview address depends on these, so they do not change.
/// </summary>
public static class SitePreviewConsts
{
    /// <summary>
    /// The query parameter of a preview link (<c>/about?site-preview=&lt;token&gt;</c>). The site exchanges it for the
    /// <see cref="CookieName"/> cookie and redirects to the same address without it.
    /// </summary>
    public const string QueryKey = SiteThemePreviewConsts.QueryKey;

    /// <summary>The HttpOnly cookie that holds the preview ticket for the rest of the visit.</summary>
    public const string CookieName = "Dignite.Site.Preview";

    /// <summary>
    /// The address that ends a preview, under the host's base path, without a leading slash:
    /// <c>/site-preview/exit?returnUrl=/about</c> deletes the cookie and goes back to <c>returnUrl</c>.
    /// </summary>
    public const string ExitPath = "site-preview/exit";

    /// <summary>The <c>id</c> of the banner a previewed page carries at the top.</summary>
    public const string BannerElementId = "site-preview-banner";
}
