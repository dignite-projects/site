using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Dignite.Site.Public.TagHelpers;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>content_counts options</c>: how many of a page's published contents carry each value of one field - the
/// counts per category beside a news list. It counts the contents <c>contents</c> would list under the same
/// options, so the two always speak of the same contents. <c>options</c> is an object, <c>field</c> required, the
/// rest optional:
/// <list type="bullet">
/// <item><c>field</c> - the name of the field to group by. It has to be one of the page's content types' fields
/// that the content type opted into search, as for a route filter; any other (or an unknown one) counts nothing.</item>
/// <item><c>page</c> - the name of the page to count. Omitted: the current page, and then the current request's
/// route filters apply as they do to <c>contents</c> - except one on <c>field</c> itself, so on
/// <c>/blog/{category}</c> the counts are those of every category, not only the current one.</item>
/// <item><c>content_type</c> - only contents of the page's content type of this name.</item>
/// <item><c>filter</c> - a text filter.</item>
/// <item><c>max</c> - the most groups, default 100, at most <see cref="MaxGroups"/>.</item>
/// <item><c>field_filters</c>, <c>published_after</c>, <c>published_before</c> - as <c>contents</c> takes them.</item>
/// </list>
/// Returns a list of <c>{ value, count }</c>, most numerous first: <c>value</c> is the field's value as text (a
/// select field's option value - its label is in the field's definition, see <c>field_definitions</c>), <c>count</c>
/// the number of contents with it. A content with several values of the field counts under each, so the counts can
/// add up to more than <c>total_count</c> of <c>contents</c>; a content with none is in no group. An unknown option
/// name is an error, so a misspelt one does not go unnoticed.
/// </summary>
public class ContentCountsFunction : SiteScriptFunction
{
    public const string Name = "content_counts";

    /// <summary>The most groups a template may ask for in one call.</summary>
    public const int MaxGroups = 1000;

    private static readonly string[] OptionNames =
    {
        "field", "page", "content_type", "filter", "max", "field_filters", "published_after", "published_before"
    };

    protected ScribanRenderScope Scope { get; }
    protected ContentListQuery Query { get; }

    public ContentCountsFunction(ScribanRenderScope scope, ContentListQuery query)
        : base(Required<object>("options"))
    {
        Scope = scope;
        Query = query;
    }

    protected override async ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        if (arguments[0] is not ScriptObject options)
        {
            throw new ScriptRuntimeException(span, "content_counts takes an object of options, e.g. content_counts { field: \"category\" }.");
        }

        if (options.GetMembers().FirstOrDefault(m => !OptionNames.Contains(m)) is { } unknown)
        {
            throw new ScriptRuntimeException(span, $"content_counts has no option `{unknown}`; the options are {string.Join(", ", OptionNames)}.");
        }

        var fieldName = GetOptionString(context, span, options, "field")
            ?? throw new ScriptRuntimeException(span, "content_counts needs the name of the field to count by, e.g. content_counts { field: \"category\" }.");

        var viewModel = Scope.ViewModel;
        var pageName = GetOptionString(context, span, options, "page");
        var isCurrentPage = pageName == null;
        var maxGroups = Math.Clamp(GetOptionInt(context, span, options, "max") ?? 100, 1, MaxGroups);

        PageDto? page = isCurrentPage ? viewModel.Page : await Query.FindPageAsync(pageName!);
        if (page == null)
        {
            return new ScriptArray();
        }

        var (fieldFilters, publishedAfter, publishedBefore) = GetFilters(context, span, options, viewModel, isCurrentPage);
        var counts = await Query.CountByFieldAsync(
            new ContentListQueryRequest
            {
                Page = page,
                CultureName = viewModel.CultureName,
                ContentTypeName = GetOptionString(context, span, options, "content_type"),
                Filter = GetOptionString(context, span, options, "filter"),
                FieldFilters = fieldFilters,
                PublishedAfter = publishedAfter,
                PublishedBefore = publishedBefore
            },
            fieldName,
            maxGroups);

        var result = new ScriptArray(counts.Count);
        foreach (var count in counts)
        {
            var item = new ScriptObject();
            item.SetValue("value", count.Value, false);
            item.SetValue("count", count.Count, false);
            result.Add(item);
        }

        return result;
    }
}
