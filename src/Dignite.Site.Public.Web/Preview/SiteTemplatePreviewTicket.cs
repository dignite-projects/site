using System;
using Dignite.Site.Templates;
using Volo.Abp;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// What a preview token and the preview cookie carry, once verified: whose site, which template, which version. The
/// version is looked up by name and number when a page renders, never trusted from the ticket itself, so a ticket
/// carries no template source.
/// </summary>
public class SiteTemplatePreviewTicket
{
    public SiteTemplatePreviewTicket(Guid? tenantId, string templateName, int versionNumber)
    {
        Check.NotNullOrWhiteSpace(templateName, nameof(templateName));

        TenantId = tenantId;
        TemplateName = SiteTemplateNames.Normalize(templateName);
        VersionNumber = versionNumber;
    }

    /// <summary>
    /// The tenant the ticket was issued for; null for the host. A ticket is honored only on that tenant's site, so a
    /// ticket for one tenant opened on another's address does nothing.
    /// </summary>
    public Guid? TenantId { get; }

    /// <summary>The template the ticket previews, normalized (<see cref="SiteTemplateNames.Normalize"/>).</summary>
    public string TemplateName { get; }

    /// <summary>The number of the version rendered in place of the published one.</summary>
    public int VersionNumber { get; }

    /// <summary>Where the version is substituted: as a page's template, the layout, a partial or an asset.</summary>
    public SiteTemplateKind Kind => SiteTemplateNames.GetKind(TemplateName);
}
