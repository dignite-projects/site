using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.TagHelpers;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>find_page name</c>: a page by its name, with its content types, in the shape of <c>page</c> plus
/// <c>content_types</c> (see <see cref="ScribanRenderModelBuilder.BuildPageWithContentTypes"/>) - the current page
/// as well as any other, so a template reaches the content types of both the same way. Empty when no active page
/// has the name. The fields of a content type are its uses of them (<c>field_id</c>, <c>required</c>, ...); their
/// definitions come from <c>field_definitions</c>. A page is looked up once per render.
/// </summary>
public class FindPageFunction : SiteScriptFunction
{
    public const string Name = "find_page";

    protected ScribanRenderScope Scope { get; }
    protected ContentListQuery Query { get; }
    protected ScribanRenderModelBuilder ModelBuilder { get; }

    public FindPageFunction(ScribanRenderScope scope, ContentListQuery query, ScribanRenderModelBuilder modelBuilder)
        : base(Required<string>("name"))
    {
        Scope = scope;
        Query = query;
        ModelBuilder = modelBuilder;
    }

    protected override async ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        if (arguments[0] is not string { Length: > 0 } name)
        {
            return null;
        }

        if (!Scope.PagesByName.TryGetValue(name, out var page))
        {
            page = await Query.FindPageAsync(name);
            Scope.PagesByName[name] = page;
        }

        if (page == null)
        {
            return null;
        }

        foreach (var field in (page.ContentTypes ?? new()).SelectMany(contentType => contentType.Fields))
        {
            Scope.NamedFieldIds.Add(field.FieldId);
        }

        return ModelBuilder.BuildPageWithContentTypes(page);
    }
}
