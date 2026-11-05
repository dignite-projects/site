using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// The theme a site's preview shows, for a site rendered by another application than the one that holds the data: the
/// site's <see cref="ISiteThemePreviewSource"/> there reads the working copy, or a release, through this service.
/// <para>
/// An integration service: it is not exposed unless the application sets <c>AbpAspNetCoreMvcOptions.ExposeIntegrationServices</c>,
/// is then served under <c>/integration-api/site-public/</c>, which API gateways are not meant to forward, and is called with
/// its own remote service, <see cref="SitePublicRemoteServiceConsts.IntegrationRemoteServiceName"/>, so a site that reaches
/// the Public API through a gateway calls this one directly. It checks no permission: every call carries a
/// <see cref="SitePreviewServiceTokenProtector"/> token, which only an application holding the shared Data Protection keys
/// can write, and is answered for the tenant and the theme that token names, and with nothing without one.
/// </para>
/// </summary>
[IntegrationService]
public interface ISiteThemePreviewIntegrationService : IApplicationService
{
    /// <summary>
    /// The theme the token names - the working copy as it is saved, or a release - as
    /// <see cref="ISiteThemePreviewSource.FindSnapshotAsync"/> reads it; null for a token that is not valid, or when the
    /// tenant has no theme or no such release.
    /// </summary>
    Task<SiteThemeSnapshotDto?> FindSnapshotAsync(SiteThemePreviewSnapshotInput input);

    /// <summary>
    /// A static file of the theme the token names, with its body; null for a token that is not valid, or when there is no
    /// static file at that path.
    /// </summary>
    Task<SiteThemeAssetDto?> FindAssetAsync(SiteThemePreviewAssetInput input);
}

/// <summary>What <see cref="ISiteThemePreviewIntegrationService.FindSnapshotAsync"/> is asked with.</summary>
public class SiteThemePreviewSnapshotInput
{
    /// <summary>A <see cref="SitePreviewServiceTokenProtector"/> token naming the tenant and the theme.</summary>
    public string Token { get; set; } = default!;
}

/// <summary>What <see cref="ISiteThemePreviewIntegrationService.FindAssetAsync"/> is asked with.</summary>
public class SiteThemePreviewAssetInput
{
    /// <summary>A <see cref="SitePreviewServiceTokenProtector"/> token naming the tenant and the theme.</summary>
    public string Token { get; set; } = default!;

    /// <summary>The static file's path in the theme, such as <c>css/site.css</c>.</summary>
    public string Path { get; set; } = default!;
}
