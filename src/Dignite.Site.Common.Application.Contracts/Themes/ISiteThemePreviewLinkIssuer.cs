using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Site.Themes;

/// <summary>
/// Signs the link that opens the site in a browser with the working copy, or one release, in place of the live
/// release - the "preview" of the administration side. The link carries a short-lived token; the site exchanges it
/// for a preview cookie and renders every page the visitor then opens with that theme, under a banner and with
/// <c>noindex</c>, until the preview is exited or expires.
/// <para>
/// Declared here for the same reason as <see cref="ISiteThemePreviewRenderer"/>: the caller is
/// <c>Dignite.Site.Admin.Application</c>, the implementation is in <c>Dignite.Site.Public.Web</c>, which signs and
/// verifies the token with the application's Data Protection keys - the same keys, since it is the site that honors
/// the link. An in-process hook, not an application service: no controller, no client proxy. An application without
/// <c>Dignite.Site.Public.Web</c> has no implementation, and a link there fails with
/// <see cref="SiteErrorCodes.SiteTemplatePreviewUnavailable"/>.
/// </para>
/// </summary>
public interface ISiteThemePreviewLinkIssuer
{
    /// <summary>
    /// The absolute address of <see cref="SiteThemePreviewLinkRequest.Path"/> on the site, carrying a token for the
    /// theme the request names, valid for a few minutes.
    /// </summary>
    Task<SiteThemePreviewLinkDto> IssueAsync(SiteThemePreviewLinkRequest request, CancellationToken cancellationToken = default);
}

/// <summary>What <see cref="ISiteThemePreviewLinkIssuer"/> signs: a theme to preview, and the page to open it on.</summary>
public class SiteThemePreviewLinkRequest
{
    /// <summary>The release to preview, or null for the working copy - as saved, read again on every page.</summary>
    public Guid? ReleaseId { get; set; }

    /// <summary>
    /// The site path to open, as a visitor would request it: <c>/about</c>, <c>/ja/blog/hello</c>, a language prefix
    /// included. Null or empty is the home page.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// The site's origin, without a trailing slash (<c>https://acme.com</c>, or <c>https://acme.com/site</c> under a
    /// path), when the caller knows it. Null lets the issuer take it from the request it is called from.
    /// </summary>
    public string? BaseUrl { get; set; }
}

/// <summary>A signed preview link - see <see cref="ISiteThemePreviewLinkIssuer"/>.</summary>
public class SiteThemePreviewLinkDto
{
    /// <summary>The absolute address to open, token included.</summary>
    public string Url { get; set; } = default!;

    /// <summary>The site path it opens, normalized to start with a slash.</summary>
    public string Path { get; set; } = default!;

    /// <summary>The release it previews, or null for the working copy.</summary>
    public Guid? ReleaseId { get; set; }

    /// <summary>
    /// When the token stops being accepted (UTC). Opening the link before then starts a preview that lasts a couple of
    /// hours; after, the link opens the live site.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
