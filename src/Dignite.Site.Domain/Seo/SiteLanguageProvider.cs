using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace Dignite.Site.Seo;

/// <summary>
/// Reads <see cref="SiteSettings.EnabledLanguages"/> and answers which languages this site serves and
/// which of them is the default (总体设计 §5.5). The parsing itself - order, normalization,
/// de-duplication, the first entry being the default - is <see cref="SiteLanguages.ParseCultureNames"/>,
/// shared with the rendering side.
/// </summary>
public class SiteLanguageProvider : DomainService
{
    /// <summary>Used when the setting is blank or contains nothing recognizable.</summary>
    public const string FallbackCultureName = SiteLanguages.FallbackCultureName;

    protected ISettingProvider SettingProvider { get; }

    public SiteLanguageProvider(ISettingProvider settingProvider)
    {
        SettingProvider = settingProvider;
    }

    /// <summary>
    /// The site's languages in configured order, normalized and de-duplicated. Never empty: an
    /// unparseable setting falls back to <see cref="FallbackCultureName"/> rather than leaving the site
    /// with no language at all.
    /// </summary>
    public virtual async Task<IReadOnlyList<string>> GetEnabledLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        return SiteLanguages.ParseCultureNames(await SettingProvider.GetOrNullAsync(SiteSettings.EnabledLanguages));
    }

    /// <summary>The language whose URLs carry no culture prefix - the first enabled one.</summary>
    public virtual async Task<string> GetDefaultLanguageAsync(CancellationToken cancellationToken = default)
    {
        return (await GetEnabledLanguagesAsync(cancellationToken))[0];
    }
}
