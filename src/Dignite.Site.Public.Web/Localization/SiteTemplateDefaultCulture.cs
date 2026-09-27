using System;
using System.Threading;
using Volo.Abp;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The site's default language for the template being rendered - the last culture
/// <see cref="SiteTemplateLocalizationContributor"/> tries before giving up on a text.
/// <para>
/// Ambient rather than looked up, because a localization contributor answers synchronously and the
/// language comes from an async call. <see cref="SiteLanguageResultFilter"/> sets it around every view MVC
/// renders, Site pages included, in the same async flow. Outside a view render it is null and that step is
/// skipped.
/// </para>
/// </summary>
public static class SiteTemplateDefaultCulture
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string? Current => CurrentValue.Value;

    /// <summary>Sets <see cref="Current"/> until the returned scope is disposed, then restores the previous value.</summary>
    public static IDisposable Use(string? cultureName)
    {
        var previous = CurrentValue.Value;
        CurrentValue.Value = cultureName;
        return new DisposeAction(() => CurrentValue.Value = previous);
    }
}
