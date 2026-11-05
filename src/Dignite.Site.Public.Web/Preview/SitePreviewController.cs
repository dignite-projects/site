using System.Threading.Tasks;
using Asp.Versioning;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The addresses of the site's preview that are not pages of the site. Literal routes with the default <c>Order = 0</c>,
/// so <see cref="Routing.SiteRenderController"/>'s <c>Order = 1</c> catch-all never sees them.
/// <list type="bullet">
/// <item>
/// <c>GET /site-preview/exit?returnUrl=/about</c>, the banner's link, deletes the preview cookie and goes back to
/// <c>returnUrl</c> - which then renders with the live release. Anonymous, like the site: deleting a cookie needs no
/// permission, and the cookie is HttpOnly, so this is the only way a page can end it. Only a local <c>returnUrl</c> is
/// followed; anything else - an absolute address, <c>//host</c> - goes to the home page, so the link cannot be turned into
/// an open redirect.
/// </item>
/// <item>
/// <c>POST /site-preview/render</c> renders a page with a theme snapshot for an administration API deployed apart from the
/// site - see <see cref="SitePreviewRenderHandler"/>. Without a valid token it answers 404, as if it were not there.
/// </item>
/// </list>
/// </summary>
[ControllerName(ControllerName)]
public class SitePreviewController : AbpController
{
    public const string ControllerName = "SitePreview";

    [HttpGet("/" + SitePreviewConsts.ExitPath)]
    public virtual IActionResult Exit(string? returnUrl)
    {
        Response.Cookies.Delete(SitePreviewConsts.CookieName, SitePreviewHttp.CreateCookieOptions(Request));
        SitePreviewHttp.ApplyHeaders(Response);

        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
    }

    /// <summary>
    /// Called by a server, not a browser: no cookie, so no antiforgery token either - the service token is what
    /// authenticates the call.
    /// </summary>
    [HttpPost("/" + SiteThemePreviewConsts.RenderPath)]
    [IgnoreAntiforgeryToken]
    public virtual async Task<IActionResult> Render([FromBody] SiteThemePreviewRenderInput input)
    {
        SitePreviewHttp.ApplyHeaders(Response);

        var result = await LazyServiceProvider.LazyGetRequiredService<SitePreviewRenderHandler>()
            .HandleAsync(input, HttpContext.RequestAborted);

        return result == null ? NotFound() : Ok(result);
    }
}
