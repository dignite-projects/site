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
/// <c>contents [options]</c>: one page's published contents in the current language - the Scriban counterpart
/// of <c>&lt;content-list&gt;</c>, querying through the same <see cref="ContentListQuery"/>. <c>options</c> is an
/// object, every member optional:
/// <list type="bullet">
/// <item><c>page</c> - the name of the page to list. Omitted: the current page, and then the current request's
/// route filters apply too (<c>field_filters</c>, <c>published_after</c>/<c>published_before</c>), so an
/// archive or category page lists its own slice. A page of that name not existing lists nothing.</item>
/// <item><c>content_type</c> - only contents of the page's content type of this name.</item>
/// <item><c>max</c> - page size, default 20, at most <see cref="MaxPageSize"/>.</item>
/// <item><c>page_no</c> - 1-based page number, default 1, at most <see cref="MaxPageNumber"/>.</item>
/// <item><c>filter</c> - a text filter.</item>
/// <item><c>field_filters</c> - an object of field names and values, as a route's placeholders filter: only contents
/// whose field holds the value (<c>{ category: "news", featured: true }</c>). The field must be one the content type
/// marks Searchable, or the filter is ignored, as a route's is. On the current page it is added to the route's own.</item>
/// <item><c>published_after</c>, <c>published_before</c> - dates bounding <c>publish_time</c>, both inclusive, as a
/// route's <c>publishTime</c> placeholders bound it. On the current page they replace the route's.</item>
/// </list>
/// With <c>page</c>, none of the current route's filters apply: a template lists another page's slice by passing them,
/// <c>contents { page: "blog-post", field_filters: field_filters, published_after: published_after,
/// published_before: published_before }</c>, as an archive page listing the posts of a child page does.
/// Returns <c>{ items, total_count, max_result_count, current_page }</c>; each item has the shape of
/// <c>content</c> (<see cref="ScribanRenderModelBuilder.BuildContent(Routing.ContentRenderViewModel)"/>), its
/// <c>field_list</c> holding only the fields the content type shows in lists. An unknown option name is an
/// error, so a misspelt one does not go unnoticed.
/// </summary>
public class ContentsFunction : SiteScriptFunction
{
    public const string Name = "contents";

    /// <summary>The largest page a template may ask for in one call.</summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// The highest <c>page_no</c> a template may ask for: the last page whose skip count,
    /// <c>(page_no - 1) * max</c>, still fits an <see cref="int"/> at the largest page size. Beyond it the query's
    /// skip count would overflow into a negative number, which the content service refuses as an invalid request -
    /// an error that names no template. A page past the end lists nothing either way.
    /// </summary>
    public const int MaxPageNumber = int.MaxValue / MaxPageSize;

    private static readonly string[] OptionNames =
    {
        "page", "content_type", "max", "page_no", "filter", "field_filters", "published_after", "published_before"
    };

    protected ScribanRenderScope Scope { get; }
    protected ContentListQuery Query { get; }
    protected ScribanRenderModelBuilder ModelBuilder { get; }

    public ContentsFunction(ScribanRenderScope scope, ContentListQuery query, ScribanRenderModelBuilder modelBuilder)
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
            throw new ScriptRuntimeException(span, "contents takes an object of options, e.g. contents { page: \"blog\", max: 5 }.");
        }

        var options = arguments[0] as ScriptObject;
        if (options != null && options.GetMembers().FirstOrDefault(m => !OptionNames.Contains(m)) is { } unknown)
        {
            throw new ScriptRuntimeException(span, $"contents has no option `{unknown}`; the options are {string.Join(", ", OptionNames)}.");
        }

        var viewModel = Scope.ViewModel;
        var pageName = GetOptionString(context, span, options, "page");
        var isCurrentPage = pageName == null;
        var maxResultCount = Math.Clamp(GetOptionInt(context, span, options, "max") ?? 20, 1, MaxPageSize);
        var currentPage = Math.Clamp(GetOptionInt(context, span, options, "page_no") ?? 1, 1, MaxPageNumber);

        PageDto? page = isCurrentPage ? viewModel.Page : await Query.FindPageAsync(pageName!);
        if (page == null)
        {
            return BuildResult(new ScriptArray(), 0, maxResultCount, currentPage);
        }

        var (fieldFilters, publishedAfter, publishedBefore) = GetFilters(context, span, options, viewModel, isCurrentPage);
        var list = await Query.QueryAsync(new ContentListQueryRequest
        {
            Page = page,
            CultureName = viewModel.CultureName,
            ContentTypeName = GetOptionString(context, span, options, "content_type"),
            Filter = GetOptionString(context, span, options, "filter"),
            FieldFilters = fieldFilters,
            PublishedAfter = publishedAfter,
            PublishedBefore = publishedBefore,
            MaxResultCount = maxResultCount,
            CurrentPage = currentPage
        });

        var items = new ScriptArray(list.Items.Count);
        foreach (var item in list.Items)
        {
            items.Add(ModelBuilder.BuildContent(item));
        }

        return BuildResult(items, list.TotalCount, list.MaxResultCount, list.CurrentPage);
    }

    protected static ScriptObject BuildResult(ScriptArray items, int totalCount, int maxResultCount, int currentPage)
    {
        var result = new ScriptObject();
        result.SetValue("items", items, false);
        result.SetValue("total_count", totalCount, false);
        result.SetValue("max_result_count", maxResultCount, false);
        result.SetValue("current_page", currentPage, false);
        return result;
    }

}
