using Dignite.Site.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Settings;

namespace Dignite.Site.Settings;

public class SiteSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // None of these call .WithProviders(...): an empty Providers list allows every provider, so a
        // host-set Global value is the platform default and a tenant may override it - which is what
        // lets the host's own site (TenantId == null) fall back to that same Global value (总体设计 §4.3).
        context.Add(
            new SettingDefinition(SiteSettings.EnabledLanguages, "en",
                L("DisplayName:Site.EnabledLanguages"), L("Description:Site.EnabledLanguages"), true),
            // Blank by default: there is no sensible platform-wide domain, and blank is what makes the
            // request's own host the fallback (see SettingSiteBaseUrlResolver).
            new SettingDefinition(SiteSettings.PrimaryDomain, "",
                L("DisplayName:Site.PrimaryDomain"), L("Description:Site.PrimaryDomain"), true),
            new SettingDefinition(SiteSettings.EnableLlmsTxt, "false",
                L("DisplayName:Site.EnableLlmsTxt"), L("Description:Site.EnableLlmsTxt"), true),
            new SettingDefinition(SiteSettings.Robots.AllowIndexing, "true",
                L("DisplayName:Site.Robots.AllowIndexing"), L("Description:Site.Robots.AllowIndexing"), true),
            new SettingDefinition(SiteSettings.Robots.AllowAiTraining, "false",
                L("DisplayName:Site.Robots.AllowAiTraining"), L("Description:Site.Robots.AllowAiTraining"), true),
            new SettingDefinition(SiteSettings.Robots.AllowAiSearch, "true",
                L("DisplayName:Site.Robots.AllowAiSearch"), L("Description:Site.Robots.AllowAiSearch"), true),
            new SettingDefinition(SiteSettings.Branding.AppName, "",
                L("DisplayName:Site.Branding.AppName"), L("Description:Site.Branding.AppName"), true),
            new SettingDefinition(SiteSettings.Branding.LogoUrl, "",
                L("DisplayName:Site.Branding.LogoUrl"), L("Description:Site.Branding.LogoUrl"), true),
            new SettingDefinition(SiteSettings.Branding.LogoReverseUrl, "",
                L("DisplayName:Site.Branding.LogoReverseUrl"), L("Description:Site.Branding.LogoReverseUrl"), true)
        );
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SiteResource>(name);
    }
}
