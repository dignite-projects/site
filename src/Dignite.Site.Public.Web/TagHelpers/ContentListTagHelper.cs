using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.Pages;
using Dignite.Site.Public.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// Queries and renders a content list for one page - the Tier-0 counterpart to Dignite.Cms's own
/// <c>CmsEntryListTagHelper</c>. A <c>RouteMatchKindDto.Page</c> match carries no pre-fetched list
/// (总体设计 §7.4 "rendering is handed to the front end") - this <i>is</i> that front end, querying for
/// itself from inside a <c>Page.Template</c> view, e.g.
/// <c>&lt;content-list page="@Model.Page" culture-name="@Model.CultureName" partial-name="..." /&gt;</c>.
/// The query itself is <see cref="ContentListQuery"/>, shared with Scriban templates' <c>contents</c>.
/// </summary>
[HtmlTargetElement("content-list")]
public class ContentListTagHelper : TagHelper
{
    private readonly IRazorPartialRenderer _renderer;
    private readonly ContentListQuery _query;

    public ContentListTagHelper(IRazorPartialRenderer renderer, ContentListQuery query)
    {
        _renderer = renderer;
        _query = query;
    }

    /// <summary>The page to list content under. Takes priority over <see cref="PageName"/> when both are set.</summary>
    public PageDto? Page { get; set; }

    /// <summary>
    /// If no <see cref="Page"/> is given, the page is looked up by its (tenant-unique) name - for a widget
    /// that lists some other page's content, e.g. a "latest posts" block on the home page.
    /// </summary>
    public string? PageName { get; set; }

    public string? CultureName { get; set; }

    public Guid? ContentTypeId { get; set; }

    public string? Filter { get; set; }

    /// <summary>
    /// Placeholders a partial page-route match resolved short of a slug (<c>RouteMatchDto.FilterValues</c>
    /// / <c>SiteRenderViewModel.FieldFilters</c>, 总体设计 §3.4), resolved purely against FlexFields - see
    /// <see cref="ContentListQueryRequest.FieldFilters"/>. <c>PublishTime</c> is deliberately never a valid
    /// entry here: use <see cref="PublishedAfter"/>/<see cref="PublishedBefore"/> for that instead.
    /// </summary>
    public IReadOnlyDictionary<string, string>? FieldFilters { get; set; }

    /// <summary>Optional lower bound on <c>Content.PublishTime</c>, inclusive - passed straight through to <c>GetContentListInput.PublishedAfter</c>.</summary>
    public DateTime? PublishedAfter { get; set; }

    /// <summary>Optional upper bound on <c>Content.PublishTime</c>, inclusive - see <see cref="PublishedAfter"/>.</summary>
    public DateTime? PublishedBefore { get; set; }

    /// <summary>The name or path of the partial view that receives the built <see cref="ContentListRenderViewModel"/>.</summary>
    public string PartialName { get; set; } = default!;

    /// <summary>Number of returned results.</summary>
    public int MaxResultCount { get; set; } = 20;

    /// <summary>1-based page number.</summary>
    public int CurrentPage { get; set; } = 1;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var page = Page;
        if (page == null && !string.IsNullOrEmpty(PageName))
        {
            page = await _query.FindPageAsync(PageName);
        }

        if (page == null)
        {
            output.TagName = "p";
            output.Attributes.Add("class", "p-2 bg-warning text-dark");
            output.Content.SetContent(string.IsNullOrEmpty(PageName)
                ? "content-list requires either a page or a page-name attribute."
                : $"The page named '{PageName}' is null. Please check if the page name is valid");
            return;
        }

        var model = await BuildViewModelAsync(page);
        var body = await _renderer.RenderAsync(PartialName, model);

        output.TagName = null;
        output.Content.SetHtmlContent(body);
        output.Attributes.Clear();
    }

    protected virtual Task<ContentListRenderViewModel> BuildViewModelAsync(PageDto page)
    {
        return _query.QueryAsync(new ContentListQueryRequest
        {
            Page = page,
            CultureName = CultureName,
            ContentTypeId = ContentTypeId,
            Filter = Filter,
            FieldFilters = FieldFilters,
            PublishedAfter = PublishedAfter,
            PublishedBefore = PublishedBefore,
            MaxResultCount = MaxResultCount,
            CurrentPage = CurrentPage
        });
    }
}

/// <summary>
/// The @model a <see cref="ContentListTagHelper"/> partial receives. Items reuse <see cref="ContentRenderViewModel"/>
/// as-is - the same shape a detail view's own <c>Model.Content</c> carries - rather than a separate
/// list-item type: <c>ContentDto.Url</c> already gives a partial everything the old, now-removed
/// <c>ContentListItemViewModel.Url</c> duplicated.
/// </summary>
public class ContentListRenderViewModel
{
    public required IReadOnlyList<ContentRenderViewModel> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int MaxResultCount { get; init; }

    public required int CurrentPage { get; init; }
}
