using System;
using Microsoft.Extensions.Localization;
using Dignite.Site.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using Volo.Abp.Ui.Branding;

namespace Dignite.Site.Settings;

/// <summary>
/// Replaces ABP's hard-coded <c>DefaultBrandingProvider</c> with the tenant's own name/logo (总体设计
/// §4.1) - what JSON-LD's Organization/WebSite nodes name themselves after (GitHub issue #20).
/// <para>
/// Lives in <c>Dignite.Site.Domain</c> rather than a specific host project: <see cref="SiteSettings.Branding"/>
/// is defined right here, and ABP's conventional registrar picks up a
/// <c>[Dependency(ReplaceServices = true)]</c> class from <i>any</i> loaded module's own assembly - so
/// putting the implementation in the same assembly as the setting it reads means every host, and every
/// test module, that depends on <c>SiteDomainModule</c> (which is effectively all of them) gets the
/// tenant-aware answer. A copy living only in one specific host project would leave every other host, and
/// every test, silently reading ABP's own placeholder name instead.
/// </para>
/// </summary>
[Dependency(ReplaceServices = true)]
public class SiteBrandingProvider : DefaultBrandingProvider
{
    private readonly ISettingProvider _settingProvider;
    private readonly IStringLocalizer<SiteResource> _localizer;

    public SiteBrandingProvider(ISettingProvider settingProvider, IStringLocalizer<SiteResource> localizer)
    {
        _settingProvider = settingProvider;
        _localizer = localizer;
    }

    public override string AppName =>
        GetSettingOrDefault(SiteSettings.Branding.AppName, () => _localizer["Branding:DefaultAppName"]);

    public override string? LogoUrl => GetSettingOrNull(SiteSettings.Branding.LogoUrl);

    public override string? LogoReverseUrl => GetSettingOrNull(SiteSettings.Branding.LogoReverseUrl);

    // IBrandingProvider is synchronous; ISettingProvider is async. AsyncHelper.RunSync, not a bare
    // GetAwaiter().GetResult(): the latter is only safe where no SynchronizationContext exists, and this
    // class now ships inside Dignite.Site.Domain rather than a specific MVC host - a Blazor Server consumer
    // would run it under the single-threaded RendererSynchronizationContext, where blocking the calling
    // thread deadlocks the very continuation it is waiting on. AsyncHelper runs the task on a context-free
    // thread-pool thread instead, so this stays correct wherever the module is loaded.
    private string GetSettingOrDefault(string settingName, Func<string> fallback)
    {
        var value = AsyncHelper.RunSync(() => _settingProvider.GetOrNullAsync(settingName));
        return string.IsNullOrEmpty(value) ? fallback() : value;
    }

    private string? GetSettingOrNull(string settingName)
    {
        var value = AsyncHelper.RunSync(() => _settingProvider.GetOrNullAsync(settingName));
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
