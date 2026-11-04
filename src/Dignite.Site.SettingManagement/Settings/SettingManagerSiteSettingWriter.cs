using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;

namespace Dignite.Site.Settings;

/// <inheritdoc cref="ISiteSettingWriter"/>
/// <remarks>
/// A tenant's value is forced: a tenant that sets the very value the host has keeps its own, instead of following the
/// host's later changes. The host's own value is the Global one.
/// </remarks>
public class SettingManagerSiteSettingWriter : ISiteSettingWriter, ITransientDependency
{
    protected ISettingManager SettingManager { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public SettingManagerSiteSettingWriter(ISettingManager settingManager, ICurrentTenant currentTenant)
    {
        SettingManager = settingManager;
        CurrentTenant = currentTenant;
    }

    public virtual async Task SetAsync(string name, string value)
    {
        if (CurrentTenant.Id is { } tenantId)
        {
            await SettingManager.SetForTenantAsync(tenantId, name, value, forceToSet: true);
        }
        else
        {
            await SettingManager.SetGlobalAsync(name, value);
        }
    }
}
