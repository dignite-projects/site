using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Pages;
using Volo.Abp;

namespace Dignite.Site.Seo;

/// <summary>
/// The languages a site serves and how their URLs look (总体设计 §5.5): the parsed
/// <c>Site.EnabledLanguages</c> setting, plus the two directions of the culture prefix.
/// <para>
/// <b>The first entry is the default.</b> Its URLs carry no prefix; every other language lives under
/// <c>/{culture}/</c>. There is no separate "default language" setting on purpose: two settings can
/// contradict each other - a default that is not in the enabled list has no defined meaning - and ordering
/// already expresses everything a tenant needs. A site that wants French unprefixed writes <c>"fr,en"</c>.
/// </para>
/// <para>
/// <b>Both directions live here, and they have to agree</b> - the same reasoning as <see cref="PageRoute"/>
/// one level down. <see cref="ApplyCulturePrefix"/> puts the language segment on when a URL is emitted
/// (sitemap, feeds, canonical, hreflang, in-site links); <see cref="TryStripCulturePrefix"/> takes it off
/// again when a request is routed. Split across two places they would drift, and the site would advertise
/// URLs it then 404s on. This class is in Domain.Shared so that the Domain (<c>SiteUrlContext</c>) and the
/// rendering side (<c>Dignite.Site.Public.Web</c>, which must not reference the Domain) share one copy
/// rather than each keeping its own.
/// </para>
/// </summary>
public class SiteLanguages
{
    /// <summary>Used when the setting is blank or contains nothing recognizable.</summary>
    public const string FallbackCultureName = "en";

    /// <param name="defaultCultureName">The language whose URLs carry no prefix.</param>
    /// <param name="enabledCultureNames">
    /// Normalized, and normally containing <paramref name="defaultCultureName"/> as its first entry - which
    /// <see cref="Parse"/> guarantees.
    /// </param>
    public SiteLanguages(string defaultCultureName, IReadOnlyList<string> enabledCultureNames)
    {
        DefaultCultureName = Check.NotNullOrWhiteSpace(defaultCultureName, nameof(defaultCultureName));
        EnabledCultureNames = Check.NotNull(enabledCultureNames, nameof(enabledCultureNames));
    }

    /// <summary>The language whose URLs carry no prefix.</summary>
    public string DefaultCultureName { get; }

    /// <summary>In configured order, normalized; the first entry is <see cref="DefaultCultureName"/>.</summary>
    public IReadOnlyList<string> EnabledCultureNames { get; }

    /// <summary>Builds the languages from the setting's raw value - see <see cref="ParseCultureNames"/>.</summary>
    public static SiteLanguages Parse(string? enabledLanguages)
    {
        var cultureNames = ParseCultureNames(enabledLanguages);
        return new SiteLanguages(cultureNames[0], cultureNames);
    }

    /// <summary>
    /// The setting's comma-separated culture names in configured order, normalized and de-duplicated.
    /// Never empty: an unparseable setting falls back to <see cref="FallbackCultureName"/> rather than
    /// leaving the site with no language at all.
    /// </summary>
    public static IReadOnlyList<string> ParseCultureNames(string? enabledLanguages)
    {
        var cultureNames = new List<string>();

        foreach (var candidate in (enabledLanguages ?? string.Empty).Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Unrecognized entries are skipped rather than stored: the same predefinedOnly reasoning as
            // CultureNameNormalizer - a typo must not become a phantom language with its own URL space.
            // List<string>.Contains compares with EqualityComparer<string>.Default, which is ordinal -
            // the comparison these canonical tags need.
            if (CultureNameNormalizer.TryNormalize(candidate, out var normalized)
                && !cultureNames.Contains(normalized))
            {
                cultureNames.Add(normalized);
            }
        }

        if (cultureNames.Count == 0)
        {
            cultureNames.Add(FallbackCultureName);
        }

        return cultureNames;
    }

    /// <summary>
    /// Whether this site actually serves <paramref name="cultureName"/> - the one question every emitter of
    /// a language-prefixed URL has to ask before advertising it.
    /// <para>
    /// It exists because the two directions are <b>not</b> symmetric, and cannot be made so without a
    /// contract change: <see cref="ApplyCulturePrefix"/> is total and will happily prefix any culture,
    /// while <see cref="TryStripCulturePrefix"/> refuses to strip one the tenant does not serve. A caller
    /// that skips this check publishes URLs this same site then 404s on - and content in a non-served
    /// language is entirely legal, since <c>ContentManager</c> normalizes <c>CultureName</c> without
    /// validating it against the enabled list, and a language can be dropped from that list long after
    /// content was written in it (GitHub issue #35).
    /// </para>
    /// </summary>
    public bool IsServed(string? cultureName)
    {
        return CultureNameNormalizer.TryNormalize(cultureName, out var normalized)
               && (IsDefault(normalized) || IsEnabled(normalized));
    }

    /// <summary>
    /// Prefixes <paramref name="path"/> with the language segment, unless it is the default language.
    /// </summary>
    /// <param name="path">A site-relative path - a page route or content path - normalized to a leading slash.</param>
    public string ApplyCulturePrefix(string path, string cultureName)
    {
        var normalizedPath = PageRoute.Normalize(path);

        if (!CultureNameNormalizer.TryNormalize(cultureName, out var normalizedCulture)
            || IsDefault(normalizedCulture))
        {
            return normalizedPath;
        }

        // "/" would otherwise produce "/zh-Hans/", a second URL for the same page.
        return normalizedPath == "/" ? "/" + normalizedCulture : "/" + normalizedCulture + normalizedPath;
    }

    /// <summary>
    /// The reverse of <see cref="ApplyCulturePrefix"/>: reads a language segment off the front of a
    /// site-relative request path. When it returns false, <paramref name="cultureName"/> is the default
    /// language and <paramref name="remainingPath"/> the whole path, normalized - an unprefixed URL means
    /// "default language, path as-is".
    /// <para>
    /// Only strips a culture that is <b>enabled and not the default</b>, which is what keeps a page whose
    /// route happens to be <c>/en</c> from being mistaken for an English prefix - <c>en</c> only wins if
    /// the tenant actually serves English as a non-default language.
    /// </para>
    /// </summary>
    public bool TryStripCulturePrefix(string? path, out string cultureName, out string remainingPath)
    {
        cultureName = DefaultCultureName;
        remainingPath = PageRoute.Normalize(string.IsNullOrWhiteSpace(path) ? "/" : path!);

        if (remainingPath == "/")
        {
            return false;
        }

        var firstSlash = remainingPath.IndexOf('/', 1);
        var firstSegment = firstSlash < 0 ? remainingPath[1..] : remainingPath[1..firstSlash];

        if (!CultureNameNormalizer.TryNormalize(firstSegment, out var normalizedCulture)
            || IsDefault(normalizedCulture)
            || !IsEnabled(normalizedCulture))
        {
            return false;
        }

        cultureName = normalizedCulture;
        remainingPath = firstSlash < 0 ? "/" : PageRoute.Normalize(remainingPath[firstSlash..]);

        return true;
    }

    private bool IsDefault(string normalizedCulture)
    {
        return string.Equals(normalizedCulture, DefaultCultureName, StringComparison.Ordinal);
    }

    private bool IsEnabled(string normalizedCulture)
    {
        return EnabledCultureNames.Contains(normalizedCulture, StringComparer.Ordinal);
    }
}
