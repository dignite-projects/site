using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Templating;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using Microsoft.Extensions.Localization;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>t key [fallback]</c>, also <c>{{ "Blog:Name" | t }}</c>: the site's own template text named <c>key</c>
/// (<see cref="SiteTemplateResource"/>, GitHub issue #73), in the page's language with the site's default
/// language behind it - or <c>fallback</c>, or failing that the key itself, when the site has not written it
/// (<c>LocalizerTemplateExtensions.GetOrDefault</c>). Written HTML-encoded, like every value; a text that
/// holds markup on purpose is written with <c>{{ t "key" | html_raw }}</c>.
/// </summary>
public class TextFunction : SiteScriptFunction
{
    public const string Name = "t";

    protected IStringLocalizer<SiteTemplateResource> Localizer { get; }

    public TextFunction(IStringLocalizer<SiteTemplateResource> localizer)
        : base(Required<string>("key"), Optional<string>("fallback"))
    {
        Localizer = localizer;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var key = arguments[0] as string;
        if (string.IsNullOrEmpty(key))
        {
            return new ValueTask<object?>(string.Empty);
        }

        return new ValueTask<object?>(Localizer.GetOrDefault(key, arguments[1] as string));
    }
}
