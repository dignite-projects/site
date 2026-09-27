using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Seo;
using Dignite.Site.Seo;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The current site's languages for one request (GitHub issue #75): <c>Site.EnabledLanguages</c>, parsed by
/// the same <see cref="SiteLanguages"/> the Domain routes with, and the language the request is in.
/// <para>
/// <b>Loaded on first use, not by middleware.</b> The setting is per tenant, so it must not be read before
/// the tenant is resolved - and a middleware cannot make sure it runs after <c>UseMultiTenancy()</c>. Every
/// caller here runs inside MVC instead, which by construction runs after it: the tag helpers load it
/// themselves (<see cref="GetLanguagesAsync"/>), and <see cref="SiteLanguageResultFilter"/> loads it before
/// any view renders, which is what lets synchronous callers such as <c>Url.SiteContent</c> use
/// <see cref="Languages"/>. A request that renders no view - an API call - never reads the setting.
/// </para>
/// <para>
/// The setting is visible to clients, so this works the same in-process and in a tiered host, where
/// <see cref="ISettingProvider"/> reads ABP's cached application configuration. A failure to read it is not
/// caught: the Site API that routes the same request reads the same setting, and a rendering side that
/// quietly fell back to one language would disagree with it on every link.
/// </para>
/// </summary>
public class SiteLanguageContext : IScopedDependency
{
    // Keyed by tenant - the host under Guid.Empty - so a view that switches tenant (CurrentTenant.Change)
    // neither sees the other tenant's list nor loses its own once the switch is over.
    private readonly Dictionary<Guid, SiteLanguages> _languagesByTenant = new();

    protected ISettingProvider SettingProvider { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public SiteLanguageContext(
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        IHttpContextAccessor httpContextAccessor)
    {
        SettingProvider = settingProvider;
        CurrentTenant = currentTenant;
        HttpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// The current tenant's languages, once <see cref="GetLanguagesAsync"/> has loaded them or
    /// <see cref="SetLanguages"/> has supplied them; null before that.
    /// </summary>
    public SiteLanguages? Languages => _languagesByTenant.GetValueOrDefault(CurrentTenantKey);

    /// <summary>
    /// The head metadata of the Site page this request renders, recorded by <c>SiteRenderController</c> -
    /// what <see cref="SiteLanguageSwitcher"/> finds the current page's translations in. Null on every other
    /// page.
    /// </summary>
    public HeadMetadataDto? HeadMetadata { get; set; }

    /// <summary>The current site's languages, read once per request (and tenant).</summary>
    public virtual async Task<SiteLanguages> GetLanguagesAsync()
    {
        var tenantKey = CurrentTenantKey;
        if (_languagesByTenant.TryGetValue(tenantKey, out var loaded))
        {
            return loaded;
        }

        var languages = SiteLanguages.Parse(await SettingProvider.GetOrNullAsync(SiteSettings.EnabledLanguages));
        _languagesByTenant[tenantKey] = languages;

        return languages;
    }

    /// <summary>
    /// Supplies the current tenant's languages from an answer that already carries them - the
    /// <c>RouteMatchDto</c> a Site page was resolved with - so the setting is not read a second time.
    /// </summary>
    public virtual void SetLanguages(SiteLanguages languages)
    {
        _languagesByTenant[CurrentTenantKey] = languages;
    }

    /// <summary>
    /// <see cref="Languages"/> for a synchronous caller, which cannot load them itself.
    /// </summary>
    /// <exception cref="InvalidOperationException">They have not been loaded - the caller runs outside a view.</exception>
    public virtual SiteLanguages GetLoadedLanguages()
    {
        return Languages ?? throw new InvalidOperationException(
            "The site's languages have not been loaded for this request. They are loaded before a view " +
            "renders; code that runs elsewhere, e.g. in a controller action, has to await " +
            $"{nameof(SiteLanguageContext)}.{nameof(GetLanguagesAsync)}() first.");
    }

    /// <summary>The language the current request is in - see <see cref="GetCurrentCultureName"/>.</summary>
    public virtual async Task<string> GetCurrentCultureNameAsync()
    {
        return GetCurrentCultureName(await GetLanguagesAsync());
    }

    /// <summary>
    /// The language named by the request path's first segment - <see cref="SiteLanguages.TryStripCulturePrefix"/>,
    /// exactly what routing does with it - and otherwise the default language.
    /// <para>
    /// The URL, not <c>CultureInfo.CurrentUICulture</c>: on a Site page the two agree, but elsewhere the UI
    /// culture is whatever request localization picked - typically the admin side's culture cookie - which
    /// says nothing about the language the visitor is reading the site in. When an error page is rendered by
    /// re-executing the request (<c>UseStatusCodePagesWithReExecute</c>, <c>UseExceptionHandler</c>), it is
    /// the original path that counts, so <c>/ja/missing</c>'s 404 page is still in Japanese.
    /// </para>
    /// </summary>
    public virtual string GetCurrentCultureName(SiteLanguages languages)
    {
        var httpContext = HttpContextAccessor.HttpContext;
        var path = httpContext?.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
                   ?? httpContext?.Features.Get<IExceptionHandlerPathFeature>()?.Path
                   ?? httpContext?.Request.Path.Value;

        languages.TryStripCulturePrefix(path, out var cultureName, out _);
        return cultureName;
    }

    private Guid CurrentTenantKey => CurrentTenant.Id ?? Guid.Empty;
}
