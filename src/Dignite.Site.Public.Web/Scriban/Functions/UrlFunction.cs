using System;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>url path [culture]</c>: an in-site link written <c>~/...</c>, in the page's language and under the
/// host's base path - on <c>/ja/about</c>, <c>url "~/services"</c> is <c>/ja/services</c>; with a
/// <c>culture</c>, in that language instead. The same rule <c>&lt;a href="~/..."&gt;</c> follows in a Razor
/// template (<see cref="SiteLinks.Localize"/>): the default language, a path that is not a Site page
/// (<see cref="SiteLanguageOptions.ExcludedPathPrefixes"/>, anything file-like) and a path that already
/// carries a language are left without a prefix. Anything not starting with <c>~/</c> is returned as is.
/// </summary>
public class UrlFunction : SiteScriptFunction
{
    public const string Name = "url";

    protected ScribanRenderScope Scope { get; }
    protected SiteLanguageContext LanguageContext { get; }
    protected IOptions<SiteLanguageOptions> LanguageOptions { get; }
    protected IUrlHelperFactory UrlHelperFactory { get; }

    public UrlFunction(
        ScribanRenderScope scope,
        SiteLanguageContext languageContext,
        IOptions<SiteLanguageOptions> languageOptions,
        IUrlHelperFactory urlHelperFactory)
        : base(Required<string>("path"), Optional<string>("culture"))
    {
        Scope = scope;
        LanguageContext = languageContext;
        LanguageOptions = languageOptions;
        UrlHelperFactory = urlHelperFactory;
    }

    protected override async ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var path = arguments[0] as string;
        if (path == null || !path.StartsWith("~/", StringComparison.Ordinal))
        {
            return path;
        }

        var languages = await LanguageContext.GetLanguagesAsync();
        var cultureName = OptionalString(arguments, 1) ?? LanguageContext.GetCurrentCultureName(languages);
        var localized = SiteLinks.Localize(path, cultureName, languages, LanguageOptions.Value.ExcludedPathPrefixes);

        return UrlHelperFactory.GetUrlHelper(Scope.ActionContext).Content(localized);
    }
}
