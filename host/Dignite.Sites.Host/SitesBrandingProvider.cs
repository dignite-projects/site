using System;
using Microsoft.Extensions.Localization;
using Dignite.Sites.Host.Localization;
using Dignite.Sites.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.Ui.Branding;

namespace Dignite.Sites.Host;

[Dependency(ReplaceServices = true)]
public class SitesBrandingProvider : DefaultBrandingProvider
{
    private readonly ISettingProvider _settingProvider;
    private readonly IStringLocalizer<HostResource> _localizer;

    public SitesBrandingProvider(ISettingProvider settingProvider, IStringLocalizer<HostResource> localizer)
    {
        _settingProvider = settingProvider;
        _localizer = localizer;
    }

    public override string AppName => GetSettingOrDefault(SitesSettings.Branding.AppName, () => _localizer["AppName"]);

    public override string? LogoUrl => GetSettingOrNull(SitesSettings.Branding.LogoUrl);

    public override string? LogoReverseUrl => GetSettingOrNull(SitesSettings.Branding.LogoReverseUrl);

    // IBrandingProvider is synchronous; ISettingProvider is async. Bridging with GetAwaiter().GetResult()
    // is safe here - branding is read once per request/render, not a hot loop, and Kestrel carries no
    // SynchronizationContext to deadlock against.
    private string GetSettingOrDefault(string settingName, Func<string> fallback)
    {
        var value = _settingProvider.GetOrNullAsync(settingName).GetAwaiter().GetResult();
        return string.IsNullOrEmpty(value) ? fallback() : value;
    }

    private string? GetSettingOrNull(string settingName)
    {
        var value = _settingProvider.GetOrNullAsync(settingName).GetAwaiter().GetResult();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
