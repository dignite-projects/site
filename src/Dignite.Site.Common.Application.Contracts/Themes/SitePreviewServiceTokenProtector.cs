using System;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Themes;

/// <summary>
/// The tokens of a preview's server-to-server calls, when the administration API and the site are two applications:
/// the administration API asks the site to render a page (<see cref="SiteThemePreviewConsts.RenderPath"/>), and the site
/// asks the administration side's application for the theme a preview cookie names
/// (<c>ISiteThemePreviewIntegrationService</c>). Each call carries a token written just for it, which names the tenant
/// and the theme and lives <see cref="Lifetime"/>.
/// <para>
/// A purpose of its own (<see cref="Purpose"/>), so a browser preview's token - a link that can be pasted and leak - is
/// worth nothing on these calls, and a token of these calls, which never leaves the servers, nothing in a browser. Both
/// applications must share one Data Protection key ring, as for the browser preview.
/// </para>
/// </summary>
[ExposeServices(typeof(SitePreviewServiceTokenProtector))]
public class SitePreviewServiceTokenProtector : SitePreviewTokenProtector
{
    /// <summary>The Data Protection purpose of the server-to-server tokens.</summary>
    public new const string Purpose = "Dignite.Site.ThemePreview.Service";

    /// <summary>How long a token of one call is accepted: the call is made right after the token is written.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    public SitePreviewServiceTokenProtector(IDataProtectionProvider dataProtectionProvider)
        : base(dataProtectionProvider, Purpose)
    {
    }

    /// <summary>A token for one call about <paramref name="ticket"/>, accepted for <see cref="Lifetime"/>.</summary>
    public virtual string Protect(SitePreviewTicket ticket)
    {
        return Protect(ticket, Lifetime, out _);
    }
}
