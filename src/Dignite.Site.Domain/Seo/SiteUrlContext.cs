using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Pages;
using Volo.Abp;

namespace Dignite.Site.Seo;

/// <summary>
/// Everything needed to turn a route into an absolute, language-correct URL, resolved once and then used
/// synchronously (总体设计 §4.2, §5.5).
/// <para>
/// <b>Both directions live here, and they have to agree</b> - the same reasoning as
/// <see cref="PageRoute"/> one level down. <see cref="ApplyCulturePrefix"/> puts the language
/// segment on when a URL is emitted (sitemap, feeds, and later canonical and hreflang);
/// <see cref="TryStripCulturePrefix"/> takes it off again when a request is routed. Split across two
/// classes they would drift, and the site would advertise URLs it then 404s on.
/// </para>
/// <para>
/// The strategy is §5.5's default: subdirectories, with the <b>default language unprefixed</b>. The
/// default language is the first entry of the enabled list - there is deliberately no separate setting
/// for it, because a default that is not in the enabled list has no defined meaning.
/// </para>
/// </summary>
public class SiteUrlContext
{
    public SiteUrlContext(string baseUrl, string defaultCultureName, IReadOnlyList<string> enabledCultureNames)
    {
        BaseUrl = Check.NotNullOrWhiteSpace(baseUrl, nameof(baseUrl)).TrimEnd('/');
        DefaultCultureName = Check.NotNullOrWhiteSpace(defaultCultureName, nameof(defaultCultureName));
        EnabledCultureNames = enabledCultureNames;
    }

    /// <summary>The tenant's primary origin, with no trailing slash.</summary>
    public string BaseUrl { get; }

    /// <summary>The language whose URLs carry no prefix.</summary>
    public string DefaultCultureName { get; }

    /// <summary>Normalized, and always contains <see cref="DefaultCultureName"/> as its first entry.</summary>
    public IReadOnlyList<string> EnabledCultureNames { get; }

    /// <summary>
    /// Whether this site actually serves <paramref name="cultureName"/> - the one question every emitter of
    /// a language-prefixed URL has to ask before advertising it.
    /// <para>
    /// It exists because the two directions above are <b>not</b> symmetric, and cannot be made so without a
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
    /// The absolute URL of <paramref name="path"/> in <paramref name="cultureName"/>.
    /// <para>
    /// The result is percent-encoded by <see cref="Uri"/>, which matters because slugs deliberately keep
    /// Unicode letters (总体设计 §8.3) - <c>/blog/我的旅行</c> has to reach a sitemap as
    /// <c>/blog/%E6%88%91%E7%9A%84%E6%97%85%E8%A1%8C</c> to be a legal <c>&lt;loc&gt;</c>.
    /// </para>
    /// </summary>
    public string BuildAbsolute(string path, string cultureName)
    {
        var prefixed = ApplyCulturePrefix(path, cultureName);

        // Concatenated rather than fed to Uri's relative constructor: a relative path with a leading
        // slash replaces the base path, which would silently drop a site hosted under one.
        return new Uri(BaseUrl + prefixed, UriKind.Absolute).AbsoluteUri;
    }

    /// <summary>
    /// Prefixes <paramref name="path"/> with the language segment, unless it is the default language.
    /// </summary>
    /// <param name="path">A page route or content path, normalized to a leading slash.</param>
    public string ApplyCulturePrefix(string path, string cultureName)
    {
        var normalizedPath = Page.NormalizeRoute(path);

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
    /// request path.
    /// <para>
    /// Only strips a culture that is <b>enabled and not the default</b>, which is what keeps a page whose
    /// route happens to be <c>/en</c> from being mistaken for an English prefix - <c>en</c> only wins if
    /// the tenant actually serves English as a non-default language. Callers should still try an exact
    /// route match first, the way <c>SiteRouteResolver</c>'s step 1 does.
    /// </para>
    /// </summary>
    public bool TryStripCulturePrefix(string? path, out string cultureName, out string remainingPath)
    {
        cultureName = DefaultCultureName;
        remainingPath = Page.NormalizeRoute(string.IsNullOrWhiteSpace(path) ? "/" : path!);

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
        remainingPath = firstSlash < 0 ? "/" : Page.NormalizeRoute(remainingPath[firstSlash..]);

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
