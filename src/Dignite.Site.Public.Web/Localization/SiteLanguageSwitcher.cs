using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The data behind a language switcher (GitHub issue #75): for each language the site serves, where to go
/// - the current page's translation when it has one, that language's home page otherwise. Language versions
/// are grouped by their natural key (总体设计 §2.4), the same grouping hreflang is derived from, so the
/// translations are read straight off <see cref="HeadMetadataDto.HreflangAlternates"/>.
/// <para>
/// Only the data: the markup, and the languages' labels, are the host's. In a layout:
/// <c>@inject SiteLanguageSwitcher Switcher</c>, then
/// <c>@foreach (var item in await Switcher.GetItemsAsync()) { ... }</c>.
/// </para>
/// </summary>
public class SiteLanguageSwitcher : ITransientDependency
{
    protected SiteLanguageContext LanguageContext { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public SiteLanguageSwitcher(SiteLanguageContext languageContext, IHttpContextAccessor httpContextAccessor)
    {
        LanguageContext = languageContext;
        HttpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// One item per enabled language, in configured order.
    /// </summary>
    /// <param name="headMetadata">
    /// The current page's head metadata. Defaults to the one <c>SiteRenderController</c> recorded for this
    /// request (<see cref="SiteLanguageContext.HeadMetadata"/>); on any other page there is none, and every
    /// item is a home page.
    /// </param>
    public virtual async Task<IReadOnlyList<SiteLanguageSwitchItem>> GetItemsAsync(HeadMetadataDto? headMetadata = null)
    {
        var languages = await LanguageContext.GetLanguagesAsync();
        var currentCultureName = LanguageContext.GetCurrentCultureName(languages);
        var alternates = (headMetadata ?? LanguageContext.HeadMetadata)?.HreflangAlternates
                         ?? new List<HreflangAlternateDto>();
        var pathBase = HttpContextAccessor.HttpContext?.Request.PathBase ?? PathString.Empty;

        return languages.EnabledCultureNames
            .Select(cultureName =>
            {
                var translation = alternates.FirstOrDefault(a => string.Equals(a.CultureName, cultureName, StringComparison.Ordinal));

                return new SiteLanguageSwitchItem
                {
                    CultureName = cultureName,
                    Url = translation != null
                        ? GetTranslationUrl(translation, pathBase)
                        : pathBase + languages.ApplyCulturePrefix("/", cultureName),
                    IsCurrent = string.Equals(cultureName, currentCultureName, StringComparison.Ordinal),
                    IsTranslation = translation != null
                };
            })
            .ToList();
    }

    /// <summary>
    /// The translation's site-relative path under the request's own PathBase - the same two parts a home-page
    /// item is built from, so every item links within the host the visitor is on, whatever base path the
    /// primary domain is configured with; a staging or development host, or a tenant's second domain, does
    /// not send the visitor off to the primary domain. The absolute hreflang URL only when a server too old
    /// to send the path answered.
    /// </summary>
    protected virtual string GetTranslationUrl(HreflangAlternateDto translation, PathString pathBase)
    {
        return string.IsNullOrEmpty(translation.Path) ? translation.Url : pathBase + translation.Path;
    }
}

/// <summary>One language in a language switcher - see <see cref="SiteLanguageSwitcher"/>.</summary>
public class SiteLanguageSwitchItem
{
    /// <summary>The language, normalized (<c>zh-Hans</c>) - also what the host's <c>hreflang</c>/<c>lang</c> attributes want.</summary>
    public string CultureName { get; set; } = default!;

    /// <summary>Where the switcher links to: host-relative, PathBase included.</summary>
    public string Url { get; set; } = default!;

    /// <summary>Whether this is the language the current page is in.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// True when <see cref="Url"/> is the current page's own translation; false when the page has none in
    /// this language (or the page is not a Site page) and <see cref="Url"/> is that language's home page.
    /// </summary>
    public bool IsTranslation { get; set; }
}
