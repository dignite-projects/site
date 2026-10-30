using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Web;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>render_field name [show_in_list]</c>: one field of the current content, rendered as HTML by the same
/// display partial <c>&lt;flex-field-view&gt;</c> uses in a Razor template
/// (<c>Views/Shared/FlexFields/{FieldTypeName}.cshtml</c>, model <see cref="FlexFieldViewModel"/>) - for field
/// types whose value is not plain text: rich text, files, content references. <c>show_in_list</c> renders it
/// bare, without its label. Empty when there is no current content (a list page) or it has no field of that
/// name. Returned as <see cref="ScribanRawHtml"/>: the partial's HTML is written unencoded.
/// </summary>
public class RenderFieldFunction : SiteScriptFunction
{
    public const string Name = "render_field";

    /// <summary>The same partial-name prefix <c>FlexFieldViewTagHelper</c> dispatches with.</summary>
    public const string PartialNamePrefix = "FlexFields/";

    protected ScribanRenderScope Scope { get; }
    protected IRazorPartialRenderer PartialRenderer { get; }

    public RenderFieldFunction(ScribanRenderScope scope, IRazorPartialRenderer partialRenderer)
        : base(Required<string>("name"), Optional<bool>("show_in_list", false))
    {
        Scope = scope;
        PartialRenderer = partialRenderer;
    }

    protected override async ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var name = arguments[0] as string;
        var field = Scope.ViewModel.Content?.Fields.FirstOrDefault(f => f.Name == name);
        if (field == null)
        {
            return string.Empty;
        }

        var showInList = arguments[1] is true;
        return new ScribanRawHtml(
            await PartialRenderer.RenderAsync(PartialNamePrefix + field.FieldTypeName, new FlexFieldViewModel(field, showInList)));
    }
}
