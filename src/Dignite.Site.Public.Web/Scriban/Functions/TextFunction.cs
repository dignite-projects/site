using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>t key [fallback]</c>, also <c>{{ "Blog:Name" | t }}</c>: the theme's own text named <c>key</c>
/// (<c>locales/{culture}.json</c>, <see cref="SiteThemeTextCatalog"/>), in the page's language with the parent
/// languages and the site's default language behind it (<see cref="SiteTemplateCultureChain"/>) - or
/// <c>fallback</c>, or failing that the key itself, when the theme has not written it. Written HTML-encoded, like
/// every value; a text that holds markup on purpose is written with <c>{{ t "key" | html_raw }}</c>.
/// <para>
/// The theme's texts only: the files a host ships for its Razor views (<see cref="SiteTemplateLocalizationContributor"/>)
/// are not read, so what a theme shows is what its release holds.
/// </para>
/// </summary>
public class TextFunction : SiteScriptFunction
{
    public const string Name = "t";

    protected SiteThemeTextCatalog Texts { get; }

    protected IReadOnlyList<string> CultureChain { get; }

    /// <param name="texts">The texts of the theme being rendered.</param>
    /// <param name="cultureName">The page's language.</param>
    /// <param name="defaultCultureName">The site's default language, the last one tried; null for none.</param>
    public TextFunction(SiteThemeTextCatalog texts, string cultureName, string? defaultCultureName)
        : base(Required<string>("key"), Optional<string>("fallback"))
    {
        Texts = texts;
        CultureChain = SiteTemplateCultureChain.Create(cultureName, defaultCultureName);
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var key = arguments[0] as string;
        if (string.IsNullOrEmpty(key))
        {
            return new ValueTask<object?>(string.Empty);
        }

        return new ValueTask<object?>(Texts.Find(CultureChain, key) ?? arguments[1] as string ?? key);
    }
}
