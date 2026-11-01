using System;

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
    public const string QueryKey = "site-preview";

    /// <summary>The HttpOnly cookie that holds the preview ticket for the rest of the visit.</summary>
    public const string CookieName = "Dignite.Site.Preview";

    /// <summary>
    /// The address that ends a preview, under the host's base path, without a leading slash:
    /// <c>/site-preview/exit?returnUrl=/about</c> deletes the cookie and goes back to <c>returnUrl</c>.
    /// </summary>
    public const string ExitPath = "site-preview/exit";

    /// <summary>
    /// The Data Protection purpose the token and the cookie are protected under: a value protected for any other
    /// purpose - an antiforgery token, an authentication cookie - never reads as a preview ticket, and the reverse.
    /// </summary>
    public const string ProtectorPurpose = "Dignite.Site.ThemePreview";

    /// <summary>The <c>id</c> of the banner a previewed page carries at the top.</summary>
    public const string BannerElementId = "site-preview-banner";
}

/// <summary>How long the two halves of a preview are honored - see <see cref="SitePreviewResourceFilter"/>.</summary>
public class SitePreviewOptions
{
    /// <summary>
    /// How long the token in a preview link is accepted. Short: the link is opened right after it is issued, and a link
    /// that leaks - a pasted URL, a browser history - should be worthless by the time anyone else finds it.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long the preview cookie the token is exchanged for lasts: long enough to work on the theme and look around
    /// the site, short enough that a forgotten preview ends on its own.
    /// </summary>
    public TimeSpan CookieLifetime { get; set; } = TimeSpan.FromHours(2);
}

/// <summary>
/// What a preview token and the preview cookie carry, once verified: whose site, and which theme - the working copy,
/// or a release. The theme itself is read when a page renders, never carried by the ticket.
/// </summary>
public class SitePreviewTicket
{
    public SitePreviewTicket(Guid? tenantId, Guid? releaseId)
    {
        TenantId = tenantId;
        ReleaseId = releaseId;
    }

    /// <summary>
    /// The tenant the ticket was issued for; null for the host. A ticket is honored only on that tenant's site, so a
    /// ticket for one tenant opened on another's address does nothing.
    /// </summary>
    public Guid? TenantId { get; }

    /// <summary>The release previewed, or null for the working copy.</summary>
    public Guid? ReleaseId { get; }
}
