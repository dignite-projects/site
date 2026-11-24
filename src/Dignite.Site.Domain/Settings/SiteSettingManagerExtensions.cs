using System;
using System.Threading.Tasks;
using Volo.Abp.SettingManagement;

namespace Dignite.Site.Settings;

public static class SiteSettingManagerExtensions
{
    /// <summary>
    /// Stores a setting of the current site: for the tenant, or, on the host's own site, as the value of every site that
    /// sets none of its own. A tenant's value is forced, so that a tenant that sets the very value the host has keeps its
    /// own instead of following the host's later changes. A null value removes the site's own, falling back again.
    /// </summary>
    public static Task SetForCurrentSiteAsync(this ISettingManager settingManager, Guid? tenantId, string name, string? value)
    {
        return tenantId is { } id
            ? settingManager.SetForTenantAsync(id, name, value, forceToSet: true)
            : settingManager.SetGlobalAsync(name, value);
    }
}
