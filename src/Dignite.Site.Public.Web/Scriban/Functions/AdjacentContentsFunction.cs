using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.TagHelpers;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>adjacent_contents [options]</c>: the contents either side of the one being rendered, among its page's published
/// contents in its language by <c>publish_time</c>, for "older" and "newer" links - what a Razor view gets from
/// <c>IContentPublicAppService.GetAdjacentAsync</c>. Returns <c>{ previous, next }</c>: <c>previous</c> the next
/// <i>older</i> content, <c>next</c> the next <i>newer</i> one, each shaped like an item of <c>contents</c>, or null when
/// there is none that way. Both null on a request that names no content. <c>options</c> is an object, every member
/// optional:
/// <list type="bullet">
/// <item><c>content_type</c> - only contents of the page's content type of this name.</item>
/// </list>
/// Found by publish time on the server however many contents the page holds - where finding the content among the items
/// of <c>contents</c>, at most 100 at a time, would miss it on a page that holds more.
/// </summary>
public class AdjacentContentsFunction : SiteScriptFunction
{
    public const string Name = "adjacent_contents";

    private static readonly string[] OptionNames = { "content_type" };

    protected ScribanRenderScope Scope { get; }
    protected ContentListQuery Query { get; }
    protected ScribanRenderModelBuilder ModelBuilder { get; }

    public AdjacentContentsFunction(ScribanRenderScope scope, ContentListQuery query, ScribanRenderModelBuilder modelBuilder)
        : base(Optional<object>("options"))
    {
        Scope = scope;
        Query = query;
        ModelBuilder = modelBuilder;
    }

    protected override async ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        if (arguments[0] is not (null or ScriptObject))
        {
            throw new ScriptRuntimeException(span, "adjacent_contents takes an object of options, e.g. adjacent_contents { content_type: \"post\" }.");
        }

        var options = arguments[0] as ScriptObject;
        if (options != null && options.GetMembers().FirstOrDefault(m => !OptionNames.Contains(m)) is { } unknown)
        {
            throw new ScriptRuntimeException(span, $"adjacent_contents has no option `{unknown}`; the options are {string.Join(", ", OptionNames)}.");
        }

        var result = new ScriptObject();
        result.SetValue("previous", null, false);
        result.SetValue("next", null, false);

        var viewModel = Scope.ViewModel;
        var content = viewModel.Content?.Content;
        if (content == null)
        {
            return result;
        }

        var (previous, next) = await Query.FindAdjacentAsync(
            viewModel.Page,
            content.Id,
            GetOptionString(context, span, options, "content_type"));

        if (previous != null)
        {
            result.SetValue("previous", ModelBuilder.BuildContent(previous), false);
        }

        if (next != null)
        {
            result.SetValue("next", ModelBuilder.BuildContent(next), false);
        }

        return result;
    }
}
