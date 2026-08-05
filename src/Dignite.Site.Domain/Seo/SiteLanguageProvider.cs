using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace Dignite.Site.Seo;

/// <summary>
/// Reads <see cref="SiteSettings.EnabledLanguages"/> and answers which languages this site serves and
/// which of them is the default (总体设计 §5.5).
/// <para>
/// <b>The first entry is the default.</b> There is no separate "default language" setting on purpose: two
/// settings can contradict each other - a default that is not in the enabled list has no defined meaning -
/// and ordering already expresses everything a tenant needs. A site that wants French unprefixed writes
/// <c>"fr,en"</c>.
/// </para>
/// </summary>
public class SiteLanguageProvider : DomainService
{
    /// <summary>Used when the setting is blank or contains nothing recognizable.</summary>
    public const string FallbackCultureName = "en";

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
        var configured = await SettingProvider.GetOrNullAsync(SiteSettings.EnabledLanguages);

        var languages = new List<string>();

        foreach (var candidate in (configured ?? string.Empty).Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Unrecognized entries are skipped rather than stored: the same predefinedOnly reasoning as
            // CultureNameNormalizer - a typo must not become a phantom language with its own URL space.
            // List<string>.Contains compares with EqualityComparer<string>.Default, which is ordinal -
            // the comparison these canonical tags need.
            if (CultureNameNormalizer.TryNormalize(candidate, out var normalized)
                && !languages.Contains(normalized))
            {
                languages.Add(normalized);
            }
        }

        if (languages.Count == 0)
        {
            languages.Add(FallbackCultureName);
        }

        return languages;
    }

    /// <summary>The language whose URLs carry no culture prefix - the first enabled one.</summary>
    public virtual async Task<string> GetDefaultLanguageAsync(CancellationToken cancellationToken = default)
    {
        return (await GetEnabledLanguagesAsync(cancellationToken))[0];
    }
}
