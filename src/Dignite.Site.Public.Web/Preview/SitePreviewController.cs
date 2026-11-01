using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The way out of the site's preview: <c>GET /site-preview/exit?returnUrl=/about</c>, the banner's link, deletes the
/// preview cookie and goes back to <c>returnUrl</c> - which then renders with the live release. Anonymous, like the
/// site: deleting a cookie needs no permission, and the cookie is HttpOnly, so this is the only way a page can end it.
/// <para>
/// Only a local <c>returnUrl</c> is followed; anything else - an absolute address, <c>//host</c> - goes to the home
/// page, so the link cannot be turned into an open redirect. A literal route with the default <c>Order = 0</c>, so
/// <see cref="Routing.SiteRenderController"/>'s <c>Order = 1</c> catch-all never sees it.
/// </para>
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
}
