using System.Collections.Generic;

namespace Dignite.Site.Themes;

/// <summary>
/// The fixed names of the site's preview that the administration side and the site both depend on - they may be two
/// applications - so they do not change.
/// </summary>
public static class SiteThemePreviewConsts
{
    /// <summary>
    /// The query parameter of a preview link (<c>/about?site-preview=&lt;token&gt;</c>). The site exchanges it for the
    /// preview cookie and redirects to the same address without it.
    /// </summary>
    public const string QueryKey = "site-preview";

    /// <summary>
    /// The site's render endpoint for an administration API deployed apart from the site, under the site's base path,
    /// without a leading slash: <c>POST /site-preview/render</c> with a <see cref="SiteThemePreviewRenderInput"/>,
    /// answered with a <see cref="SiteThemePreviewRenderResult"/>. The administration API has no renderer of its own, so
    /// it sends the theme snapshot to the application that serves the site and has the page rendered there, by the same
    /// renderer, with the same services, as the live page.
    /// </summary>
    public const string RenderPath = "site-preview/render";

    /// <summary>
    /// The remote service (<c>RemoteServices:SitePublicWeb:BaseUrl</c>) that names the application serving the site, to
    /// an administration API hosted without <c>Dignite.Site.Public.Web</c>. Not set, there is nothing to render with.
    /// </summary>
    public const string RendererRemoteServiceName = "SitePublicWeb";
}

/// <summary>What the administration API sends to <see cref="SiteThemePreviewConsts.RenderPath"/>.</summary>
public class SiteThemePreviewRenderInput
{
    /// <summary>
    /// A <see cref="SitePreviewServiceTokenProtector"/> token naming the tenant the page is rendered for: the site renders
    /// for that tenant, and for nobody without a valid token.
    /// </summary>
    public string Token { get; set; } = default!;

    /// <summary>The site path to render - see <see cref="SiteThemePreviewRequest.Path"/>.</summary>
    public string Path { get; set; } = default!;

    /// <summary>The theme to render it with.</summary>
    public SiteThemeSnapshotDto Snapshot { get; set; } = default!;
}

/// <summary>
/// The answer of <see cref="SiteThemePreviewConsts.RenderPath"/>: the page, or why it could not be rendered. A failure the
/// renderer reports as a business exception comes back as <see cref="Error"/>, not as an HTTP error, so the
/// administration API raises the very same exception - its code and its data - whatever the site's error handling is.
/// </summary>
public class SiteThemePreviewRenderResult
{
    public SiteThemePreviewDto? Preview { get; set; }

    public SiteThemePreviewRenderError? Error { get; set; }
}

/// <summary>A business exception of the renderer, as data: its code and its data, each value as text.</summary>
public class SiteThemePreviewRenderError
{
    public string Code { get; set; } = default!;

    public Dictionary<string, string?> Data { get; set; } = new();
}
