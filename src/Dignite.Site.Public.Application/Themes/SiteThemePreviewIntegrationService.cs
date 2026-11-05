using System;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp;
using Volo.Abp.Uow;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// Reads the theme of a preview for a site rendered by another application - see
/// <see cref="ISiteThemePreviewIntegrationService"/>. It verifies the call's token and reads, through the in-process
/// <see cref="ISiteThemePreviewSource"/>, the theme the token names, for the tenant the token names: the call comes from
/// the site's server, whose own tenant resolution is not this application's concern.
/// </summary>
[IntegrationService]
public class SiteThemePreviewIntegrationService : SitePublicAppService, ISiteThemePreviewIntegrationService
{
    protected SitePreviewServiceTokenProtector Protector { get; }
    protected ISiteThemePreviewSource PreviewSource { get; }

    public SiteThemePreviewIntegrationService(SitePreviewServiceTokenProtector protector, ISiteThemePreviewSource previewSource)
    {
        Protector = protector;
        PreviewSource = previewSource;
    }

    public virtual Task<SiteThemeSnapshotDto?> FindSnapshotAsync(SiteThemePreviewSnapshotInput input)
    {
        return ReadAsync(input?.Token, ticket => PreviewSource.FindSnapshotAsync(ticket.ReleaseId));
    }

    public virtual Task<SiteThemeAssetDto?> FindAssetAsync(SiteThemePreviewAssetInput input)
    {
        return ReadAsync(input?.Token, ticket => PreviewSource.FindAssetAsync(ticket.ReleaseId, input!.Path ?? string.Empty));
    }

    /// <summary>
    /// <paramref name="read"/> for the ticket in <paramref name="token"/>, in the tenant it names and in a unit of work of
    /// that tenant; null without a valid token.
    /// </summary>
    protected virtual async Task<T?> ReadAsync<T>(string? token, Func<SitePreviewTicket, Task<T?>> read)
        where T : class
    {
        if (!Protector.TryUnprotect(token, out var ticket))
        {
            return null;
        }

        using (CurrentTenant.Change(ticket.TenantId))
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            var result = await read(ticket);
            await uow.CompleteAsync();
            return result;
        }
    }
}
