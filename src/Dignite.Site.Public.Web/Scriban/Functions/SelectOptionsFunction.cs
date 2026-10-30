using System.Threading.Tasks;
using Dignite.Site.Public.Templating;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>select_options page field</c>: every option of a Select field, <c>[{ text, value }]</c> in configured
/// order, read from the field's definition through a page whose content type uses it
/// (<see cref="SelectFieldOptionsProvider"/>) - for a category link bar that has to show a category no content
/// in view carries. Empty when the page, or a field of that name on it, does not exist.
/// </summary>
public class SelectOptionsFunction : SiteScriptFunction
{
    public const string Name = "select_options";

    protected SelectFieldOptionsProvider OptionsProvider { get; }

    public SelectOptionsFunction(SelectFieldOptionsProvider optionsProvider)
        : base(Required<string>("page"), Required<string>("field"))
    {
        OptionsProvider = optionsProvider;
    }

    protected override async ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var result = new ScriptArray();
        if (arguments[0] is not string { Length: > 0 } pageName || arguments[1] is not string { Length: > 0 } fieldName)
        {
            return result;
        }

        foreach (var option in await OptionsProvider.GetAsync(pageName, fieldName))
        {
            var item = new ScriptObject();
            item.SetValue("text", option.Text, false);
            item.SetValue("value", option.Value, false);
            result.Add(item);
        }

        return result;
    }
}
