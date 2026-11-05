using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// The <see cref="ISiteThemePreviewSource"/> of a site rendered against a remote Public API: the theme of a preview is
/// read from the application that holds the data, through <see cref="ISiteThemePreviewIntegrationService"/>
/// (<c>RemoteServices:SitePublicIntegration</c>), with a token written for each call that names the current tenant and
/// the theme. The site has already verified the visitor's preview cookie when it asks.
/// <para>
/// Registered only where the application has no in-process source - see <see cref="SitePublicHttpApiClientModule"/>.
/// A failed call is logged and read as no theme: the page then renders with the live release under a banner that says
/// so, rather than failing for the author.
/// </para>
/// </summary>
public class SiteThemePreviewRemoteSource : ISiteThemePreviewSource
{
    protected ISiteThemePreviewIntegrationService IntegrationService { get; }
    protected SitePreviewServiceTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected ILogger<SiteThemePreviewRemoteSource> Logger { get; }

    public SiteThemePreviewRemoteSource(
        ISiteThemePreviewIntegrationService integrationService,
        SitePreviewServiceTokenProtector protector,
        ICurrentTenant currentTenant,
        ILogger<SiteThemePreviewRemoteSource> logger)
    {
        IntegrationService = integrationService;
        Protector = protector;
        CurrentTenant = currentTenant;
        Logger = logger;
    }

    public virtual Task<SiteThemeSnapshotDto?> FindSnapshotAsync(Guid? releaseId, CancellationToken cancellationToken = default)
    {
        return CallAsync(releaseId, cancellationToken,
            token => IntegrationService.FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = token }));
    }

    public virtual Task<SiteThemeAssetDto?> FindAssetAsync(Guid? releaseId, string path, CancellationToken cancellationToken = default)
    {
        return CallAsync(releaseId, cancellationToken,
            token => IntegrationService.FindAssetAsync(new SiteThemePreviewAssetInput { Token = token, Path = path }));
    }

    protected virtual async Task<T?> CallAsync<T>(
        Guid? releaseId,
        CancellationToken cancellationToken,
        Func<string, Task<T?>> call)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var token = Protector.Protect(new SitePreviewTicket(CurrentTenant.Id, releaseId));
        try
        {
            return await call(token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogWarning(exception,
                "Reading the previewed theme from the remote service '{RemoteService}' failed; the page renders with the live release.",
                SitePublicRemoteServiceConsts.IntegrationRemoteServiceName);
            return null;
        }
    }
}
