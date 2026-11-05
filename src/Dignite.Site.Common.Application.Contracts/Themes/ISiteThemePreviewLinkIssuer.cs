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
/// The implementation is <c>Dignite.Site.Admin.Application</c>'s: it signs the token with
/// <see cref="SitePreviewTokenProtector"/> and points the link at the tenant's site address, the
/// <c>Site.PrimaryDomain</c> setting. The site reads the token with the same Data Protection keys, so an administration
/// API deployed apart from the site shares the site's key ring. A hook to replace, not an application service: no
/// controller, no client proxy.
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
    /// path), when the caller knows it. Null lets the issuer take the tenant's site address, <c>Site.PrimaryDomain</c>.
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
