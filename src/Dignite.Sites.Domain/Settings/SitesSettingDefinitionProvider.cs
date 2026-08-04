using Dignite.Sites.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Settings;

namespace Dignite.Sites.Settings;

public class SitesSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // None of these call .WithProviders(...): an empty Providers list allows every provider, so a
        // host-set Global value is the platform default and a tenant may override it - which is what
        // lets the host's own site (TenantId == null) fall back to that same Global value (总体设计 §4.3).
        context.Add(
            new SettingDefinition(SitesSettings.EnabledLanguages, "en",
                L("DisplayName:Sites.EnabledLanguages"), L("Description:Sites.EnabledLanguages"), true),
            new SettingDefinition(SitesSettings.Robots.AllowIndexing, "true",
                L("DisplayName:Sites.Robots.AllowIndexing"), L("Description:Sites.Robots.AllowIndexing"), true),
            new SettingDefinition(SitesSettings.Robots.AllowAiTraining, "false",
                L("DisplayName:Sites.Robots.AllowAiTraining"), L("Description:Sites.Robots.AllowAiTraining"), true),
            new SettingDefinition(SitesSettings.Robots.AllowAiSearch, "true",
                L("DisplayName:Sites.Robots.AllowAiSearch"), L("Description:Sites.Robots.AllowAiSearch"), true)
        );
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SitesResource>(name);
    }
}
