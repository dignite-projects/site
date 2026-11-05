using System;

namespace Dignite.Site.Themes;

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
