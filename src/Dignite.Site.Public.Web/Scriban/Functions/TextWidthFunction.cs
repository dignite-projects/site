using System.Threading.Tasks;
using Dignite.Site.Seo;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>text_width text</c>: how many columns <c>text</c> takes where a search result shows it - a Chinese, Japanese or
/// Korean character two, any other one - the measure the SEO field's limits use (<see cref="SeoTextWidth"/>). For a
/// template that decides, say, whether "{title} | Brand" still fits a result's 60 columns; the same arithmetic in a
/// template's own regular expression is easy to get wrong at its edges (a dash is one column, not two).
/// </summary>
public class TextWidthFunction : SiteScriptFunction
{
    public const string Name = "text_width";

    public TextWidthFunction()
        : base(Optional<string>("text"))
    {
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        return new ValueTask<object?>(SeoTextWidth.Columns(arguments[0] as string));
    }
}
