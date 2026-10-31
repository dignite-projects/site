using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Volo.Abp;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The preview cookie's attributes and the headers of a preview response, in one place: whoever writes the cookie
/// (<see cref="SiteTemplatePreviewResourceFilter"/>) and whoever deletes it (<see cref="SiteTemplatePreviewController"/>)
/// must agree on its path, or the browser keeps it.
/// </summary>
public static class SiteTemplatePreviewHttp
{
    public const string RobotsTagHeaderName = "X-Robots-Tag";

    /// <summary>
    /// Marks a response as one no cache may keep and no search engine may index - every response of draft mode,
    /// and the redirects in and out of it: what is shown is not the site's published state, and must not be
    /// served to anyone else or end up in an index.
    /// </summary>
    public static void ApplyHeaders(HttpResponse response)
    {
        Check.NotNull(response, nameof(response));

        response.Headers[RobotsTagHeaderName] = "noindex";
        response.Headers[HeaderNames.CacheControl] = "no-store";
    }

    /// <summary>
    /// The preview cookie's options: HttpOnly - no script of the previewed templates can read the ticket;
    /// <c>SameSite=Lax</c> - sent when the author follows a link to the site or opens the preview window, never on a
    /// cross-site subrequest; <c>Secure</c> on HTTPS; scoped to the host's base path, so it also reaches
    /// <c>/site-assets/</c>. Essential: it is set only by the author's own click and does nothing but what they asked
    /// for, so a cookie-consent policy must not drop it.
    /// </summary>
    /// <param name="request">The request the cookie is written or deleted on.</param>
    /// <param name="expires">When the cookie expires; null when deleting it.</param>
    public static CookieOptions CreateCookieOptions(HttpRequest request, DateTimeOffset? expires = null)
    {
        Check.NotNull(request, nameof(request));

        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Secure = request.IsHttps,
            Path = request.PathBase.HasValue ? request.PathBase.Value : "/",
            Expires = expires,
            IsEssential = true
        };
    }
}
