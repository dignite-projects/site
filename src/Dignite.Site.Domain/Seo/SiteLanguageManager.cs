using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.SettingManagement;

namespace Dignite.Site.Seo;

/// <summary>
/// Sets the languages a site serves: the one place <see cref="SiteSettings.EnabledLanguages"/> is written, as
/// <see cref="SiteLanguageProvider"/> is the one place it is read.
/// <para>
/// The list is the setting's whole value, in order, and the order is meaningful: the first language is the default, whose
/// URLs carry no prefix (<see cref="SiteLanguages"/>). Setting a list therefore moves addresses - every page of the old
/// default now sits under its prefix and every page of the new one has lost it - and a language left out stops being
/// served, though its contents are kept (<see cref="SiteLanguages.IsServed"/>).
/// </para>
/// <para>
/// A value is refused rather than repaired, unlike a value read from the setting (<see cref="SiteLanguages.ParseCultureNames"/>,
/// which skips what it does not recognize): someone who types a language that does not exist should be told, not have the
/// site quietly serve one language fewer than they asked for. The tags are stored in their canonical form and without
/// duplicates (<see cref="CultureNameNormalizer"/>).
/// </para>
/// <para>
/// Stored for the current tenant, or, on the host's own site, as the value of every site that sets none of its own
/// (<see cref="ISettingManager"/>): a tenant's value is forced, so that a tenant that sets the very value the host has keeps
/// its own instead of following the host's later changes.
/// </para>
/// </summary>
public class SiteLanguageManager : DomainService
{
    protected ISettingManager SettingManager { get; }

    public SiteLanguageManager(ISettingManager settingManager)
    {
        SettingManager = settingManager;
    }

    /// <summary>Sets the site's languages to <paramref name="cultureNames"/>, in that order, and returns them as stored.</summary>
    /// <exception cref="BusinessException">
    /// <see cref="SiteErrorCodes.SiteLanguagesRequired"/> when there is no language in the list;
    /// <see cref="SiteErrorCodes.SiteLanguageNotRecognized"/> for a tag .NET does not know.
    /// </exception>
    public virtual async Task<IReadOnlyList<string>> SetEnabledLanguagesAsync(IEnumerable<string> cultureNames)
    {
        var languages = Normalize(cultureNames);

        var value = string.Join(',', languages);

        if (CurrentTenant.Id is { } tenantId)
        {
            await SettingManager.SetForTenantAsync(tenantId, SiteSettings.EnabledLanguages, value, forceToSet: true);
        }
        else
        {
            await SettingManager.SetGlobalAsync(SiteSettings.EnabledLanguages, value);
        }

        return languages;
    }

    /// <summary>The tags in their canonical form, in the order given, each once.</summary>
    protected virtual IReadOnlyList<string> Normalize(IEnumerable<string>? cultureNames)
    {
        var languages = new List<string>();

        foreach (var candidate in (cultureNames ?? Array.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            if (!CultureNameNormalizer.TryNormalize(candidate, out var normalized))
            {
                throw new BusinessException(SiteErrorCodes.SiteLanguageNotRecognized)
                    .WithData("CultureName", candidate.Trim());
            }

            if (!languages.Contains(normalized))
            {
                languages.Add(normalized);
            }
        }

        if (languages.Count == 0)
        {
            throw new BusinessException(SiteErrorCodes.SiteLanguagesRequired);
        }

        return languages;
    }
}
