using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.Site.Seo;

/// <summary>
/// Everything needed to turn a route into an absolute, language-correct URL, resolved once and then used
/// synchronously (总体设计 §4.2, §5.5).
/// <para>
/// The language half - which languages the site serves, and putting on and taking off the culture prefix
/// - is <see cref="SiteLanguages"/>, which every member here forwards to. Both directions of the prefix
/// have to agree, or the site advertises URLs it then 404s on; they live together there, in Domain.Shared,
/// so that the rendering side (<c>Dignite.Site.Public.Web</c>) applies the very same rules instead of a
/// copy. What this class adds is the other half of an absolute URL: the tenant's primary origin.
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
        Languages = new SiteLanguages(defaultCultureName, enabledCultureNames);
    }

    /// <summary>The tenant's primary origin, with no trailing slash.</summary>
    public string BaseUrl { get; }

    /// <summary>The site's languages, and the culture-prefix rules this class forwards to.</summary>
    public SiteLanguages Languages { get; }

    /// <summary>The language whose URLs carry no prefix.</summary>
    public string DefaultCultureName => Languages.DefaultCultureName;

    /// <summary>Normalized, and always contains <see cref="DefaultCultureName"/> as its first entry.</summary>
    public IReadOnlyList<string> EnabledCultureNames => Languages.EnabledCultureNames;

    /// <inheritdoc cref="SiteLanguages.IsServed"/>
    public bool IsServed(string? cultureName)
    {
        return Languages.IsServed(cultureName);
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

    /// <inheritdoc cref="SiteLanguages.ApplyCulturePrefix"/>
    public string ApplyCulturePrefix(string path, string cultureName)
    {
        return Languages.ApplyCulturePrefix(path, cultureName);
    }

    /// <inheritdoc cref="SiteLanguages.TryStripCulturePrefix"/>
    public bool TryStripCulturePrefix(string? path, out string cultureName, out string remainingPath)
    {
        return Languages.TryStripCulturePrefix(path, out cultureName, out remainingPath);
    }
}
