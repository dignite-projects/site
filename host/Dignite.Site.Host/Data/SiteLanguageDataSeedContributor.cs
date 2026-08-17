using System.Threading.Tasks;
using Dignite.Site.Settings;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.SettingManagement;

namespace Dignite.Site.Host.Data;

/// <summary>
/// Local-testing convenience: gives the host's own site a second language out of the box, so
/// <c>/zh-Hans/...</c> culture-prefixed URLs are reachable without clicking through Settings first.
/// Global scope, not a <see cref="SiteSettingDefinitionProvider"/> default change - the shipped default
/// stays single-language "en" for every tenant that never touches this setting.
/// </summary>
public class SiteLanguageDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ISettingManager _settingManager;

    public SiteLanguageDataSeedContributor(ISettingManager settingManager)
    {
        _settingManager = settingManager;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId != null)
        {
            return;
        }

        await _settingManager.SetGlobalAsync(SiteSettings.EnabledLanguages, "en,zh-Hans");
    }
}
