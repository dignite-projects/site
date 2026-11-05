using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Site.Themes;

/// <summary>
/// Renders a page of the site with a given theme snapshot - the working copy, or a release - in place of the live
/// release: what the administration side previews through.
/// <para>
/// Declared here, not in <c>Dignite.Site.Public.Application.Contracts</c>, because the caller is
/// <c>Dignite.Site.Admin.Application</c>, which does not reference the Public contracts, and the implementation lives
/// in <c>Dignite.Site.Public.Web</c>, which references nothing but them. A plain interface rather than an
/// application service: it is an in-process hook, not an endpoint, so it gets no controller and no client proxy - the
/// working copy is never reachable over the public surface. An administration API hosted without
/// <c>Dignite.Site.Public.Web</c> has the site render it instead, over the site's <c>POST /site-preview/render</c>
/// (<see cref="SiteThemePreviewConsts.RenderPath"/>), when <c>RemoteServices:SitePublicWeb</c> names the site's
/// application; with neither, a preview fails with <see cref="SiteErrorCodes.SiteTemplatePreviewUnavailable"/>.
/// </para>
/// </summary>
public interface ISiteThemePreviewRenderer
{
    /// <summary>The HTML <see cref="SiteThemePreviewRequest.Path"/> would be served with if the snapshot were live.</summary>
    /// <exception cref="Volo.Abp.BusinessException">
    /// <see cref="SiteErrorCodes.SiteTemplatePreviewPathNotFound"/> when no page answers the path;
    /// <see cref="SiteErrorCodes.SiteThemePreviewPageNotScriban"/> when the page's template is not in the snapshot, so a
    /// Razor view renders it; <see cref="SiteErrorCodes.SiteTemplatePreviewRenderFailed"/> when a template fails at
    /// run time or runs out of time, with its message in <c>Data["Message"]</c> and, when the failure has a place in a template of the
    /// theme, that place in <c>Data["File"]</c>, <c>Data["Line"]</c> and <c>Data["Column"]</c> (1-based); also what a render that never
    /// called <c>site_head</c> raises (<see cref="SiteErrorCodes.SiteTemplateDidNotRenderSiteHead"/>).
    /// </exception>
    Task<SiteThemePreviewDto> RenderAsync(SiteThemePreviewRequest request, CancellationToken cancellationToken = default);
}

/// <summary>What <see cref="ISiteThemePreviewRenderer"/> renders: a theme snapshot, on a page of the site.</summary>
public class SiteThemePreviewRequest
{
    /// <summary>
    /// The site path to render, as a visitor would request it: <c>/about</c>, <c>/ja/blog/hello</c>, a language prefix
    /// included. It may carry a query string for the page of a paged list, <c>/ja/blog?page=2</c>; nothing else of a
    /// query string reaches the page.
    /// </summary>
    public string Path { get; set; } = default!;

    /// <summary>The theme to render it with.</summary>
    public SiteThemeSnapshotDto Snapshot { get; set; } = default!;
}

/// <summary>A page of the site rendered with a theme snapshot.</summary>
public class SiteThemePreviewDto
{
    /// <summary>The page's HTML, as it would be served.</summary>
    public string Html { get; set; } = default!;

    /// <summary>The path that was rendered, normalized to start with a slash.</summary>
    public string Path { get; set; } = default!;

    /// <summary>The name of the page the path resolved to.</summary>
    public string PageName { get; set; } = default!;

    /// <summary>The language the path resolved to, which the page was rendered in.</summary>
    public string CultureName { get; set; } = default!;

    /// <summary>The page's <c>Template</c>: the template file the page rendered through.</summary>
    public string PageTemplate { get; set; } = default!;

    /// <summary>The layout the page rendered in, or null when it rendered without one.</summary>
    public string? Layout { get; set; }
}
