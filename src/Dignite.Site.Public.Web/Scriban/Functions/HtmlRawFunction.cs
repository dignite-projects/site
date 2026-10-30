using System.Threading.Tasks;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>html_raw value</c>, also <c>{{ value | html_raw }}</c>: writes <c>value</c> as HTML, unencoded - the
/// template author's explicit opt-out of the encoding every other value gets (<see cref="SiteTemplateContext"/>),
/// e.g. for a site text that holds markup. Never for anything a visitor can influence: route values
/// (<c>filter_values</c>, <c>head.meta_title</c>) come straight from the URL. The value is turned into its text
/// first; null writes nothing.
/// </summary>
public class HtmlRawFunction : SiteScriptFunction
{
    public const string Name = "html_raw";

    public HtmlRawFunction()
        : base(Required<object>("value"))
    {
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var value = arguments[0];
        return new ValueTask<object?>(value switch
        {
            null => null,
            ScribanRawHtml raw => raw,
            _ => new ScribanRawHtml(context.ObjectToString(value) ?? string.Empty)
        });
    }
}
