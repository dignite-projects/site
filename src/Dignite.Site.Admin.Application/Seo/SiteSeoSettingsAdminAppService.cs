using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.SettingManagement;

namespace Dignite.Site.Admin.Seo;

[Authorize(SiteAdminPermissions.Seo.Default)]
public class SiteSeoSettingsAdminAppService : SiteAdminAppService, ISiteSeoSettingsAdminAppService
{
    protected ISettingManager SettingManager { get; }

    public SiteSeoSettingsAdminAppService(ISettingManager settingManager)
    {
        SettingManager = settingManager;
    }

    public virtual async Task<SiteSeoSettingsDto> GetAsync()
    {
        return new SiteSeoSettingsDto
        {
            DefaultShareImage = NullIfBlank(await SettingProvider.GetOrNullAsync(SiteSettings.Seo.DefaultShareImage))
        };
    }

    /// <summary>
    /// Stores it for the current tenant, or globally on the host's own site, which is every site's default. An empty value
    /// removes the setting rather than storing an empty one, so a tenant falls back to the host's default again.
    /// </summary>
    [Authorize(SiteAdminPermissions.Seo.Update)]
    public virtual async Task<SiteSeoSettingsDto> UpdateAsync(UpdateSiteSeoSettingsDto input)
    {
        var value = NullIfBlank(input.DefaultShareImage);

        if (CurrentTenant.Id is { } tenantId)
        {
            await SettingManager.SetForTenantAsync(tenantId, SiteSettings.Seo.DefaultShareImage, value, forceToSet: true);
        }
        else
        {
            await SettingManager.SetGlobalAsync(SiteSettings.Seo.DefaultShareImage, value);
        }

        return new SiteSeoSettingsDto { DefaultShareImage = value };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
