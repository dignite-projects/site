using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.ContentTypes;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.ContentTypes;
using Dignite.Site.Public.Contents;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Pages;
using Dignite.Site.Public.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// Queries and renders a content list for one page - the Tier-0 counterpart to Dignite.Cms's own
/// <c>CmsEntryListTagHelper</c>. A <c>RouteMatchKindDto.Page</c> match carries no pre-fetched list
/// (总体设计 §7.4 "rendering is handed to the front end") - this <i>is</i> that front end, querying for
/// itself from inside a <c>Page.Template</c> view, e.g.
/// <c>&lt;content-list page="@Model.Page" culture-name="@Model.CultureName" partial-name="..." /&gt;</c>.
/// </summary>
[HtmlTargetElement("content-list")]
public class ContentListTagHelper : TagHelper
{
    private readonly IRazorPartialRenderer _renderer;
    private readonly IContentPublicAppService _contentAppService;
    private readonly IContentTypePublicAppService _contentTypeAppService;
    private readonly IFieldPublicAppService _fieldAppService;
    private readonly IPagePublicAppService _pageAppService;
    private readonly IFieldTypeResolver _fieldTypeResolver;

    public ContentListTagHelper(
        IRazorPartialRenderer renderer,
        IContentPublicAppService contentAppService,
        IContentTypePublicAppService contentTypeAppService,
        IFieldPublicAppService fieldAppService,
        IPagePublicAppService pageAppService,
        IFieldTypeResolver fieldTypeResolver)
    {
        _renderer = renderer;
        _contentAppService = contentAppService;
        _contentTypeAppService = contentTypeAppService;
        _fieldAppService = fieldAppService;
        _pageAppService = pageAppService;
        _fieldTypeResolver = fieldTypeResolver;
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
    /// / <c>SiteRenderViewModel.FieldFilters</c>, 总体设计 §3.4), resolved purely against FlexFields - e.g.
    /// <c>{"category": "news"}</c> for a request matched against a page routed
    /// <c>/blog/{category}/{slug}</c>. Each entry names a business field, typed via its field type's own
    /// <c>IndexValueType</c> and gated on the content type having opted it into <c>Searchable</c>
    /// (总体设计 §2.3), and becomes one or more <see cref="FlexFieldQueryCondition"/>s - range-shaped when
    /// the field is itself DateTime-typed with a FORMAT. An entry naming an unknown or non-searchable field
    /// is silently ignored, never an error - a route filter value is untrusted input and must never fail
    /// the whole render.
    /// <para>
    /// <c>PublishTime</c> is deliberately never a valid entry here - it is <c>Content</c>'s own system
    /// field, not a FlexFields field (总体设计 §2.4), and <c>SiteRenderFilterValueMapper</c> carves it out
    /// upstream before <c>SiteRenderViewModel.FieldFilters</c> is even built. Use
    /// <see cref="PublishedAfter"/>/<see cref="PublishedBefore"/> for that instead.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string>? FieldFilters { get; set; }

    /// <summary>Optional lower bound on <c>Content.PublishTime</c>, inclusive - passed straight through to <see cref="GetContentListInput.PublishedAfter"/>.</summary>
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
            page = await _pageAppService.FindByNameAsync(PageName);
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

    protected virtual async Task<ContentListRenderViewModel> BuildViewModelAsync(PageDto page)
    {
        // Non-null here means some earlier read (GetAsync/GetByRouteAsync/FindByNameAsync) already fetched
        // it - route resolution's own PageDto mapping leaves it null on purpose (总体设计 §2.3). The
        // PageName fallback above always hits this (FindByNameAsync uses includeDetails: true), and a
        // caller passing in an already-fetched Page some other way benefits too - only a route-resolved
        // Page still pays for this round trip.
        var contentTypes = page.ContentTypes ?? (await _contentTypeAppService.GetListByPageAsync(page.Id)).Items;
        var contentTypesById = contentTypes.ToDictionary(ct => ct.Id);

        var fieldsById = await GetFieldsByIdAsync(
            contentTypesById.Values.SelectMany(ct => ct.Fields).Select(f => f.FieldId));

        var input = new GetContentListInput
        {
            PageId = page.Id,
            CultureName = CultureName,
            ContentTypeId = ContentTypeId,
            Filter = Filter,
            PublishedAfter = PublishedAfter,
            PublishedBefore = PublishedBefore,
            MaxResultCount = MaxResultCount,
            SkipCount = (CurrentPage - 1) * MaxResultCount
        };

        ApplyFieldFilters(input, contentTypesById.Values, fieldsById);

        var contents = await _contentAppService.GetListAsync(input);

        var items = new List<ContentRenderViewModel>(contents.Items.Count);
        foreach (var content in contents.Items)
        {
            if (!contentTypesById.TryGetValue(content.ContentTypeId, out var contentType))
            {
                continue; // content type removed since this content was written - skip, don't break the list
            }

            items.Add(new ContentRenderViewModel
            {
                Content = content,
                ContentType = contentType,
                Fields = SiteRenderFieldMapper.Build(content, contentType, fieldsById, listFieldsOnly: true)
            });
        }

        return new ContentListRenderViewModel
        {
            Items = items,
            TotalCount = (int)contents.TotalCount,
            MaxResultCount = MaxResultCount,
            CurrentPage = CurrentPage
        };
    }

    protected virtual async Task<IReadOnlyDictionary<Guid, FieldDto>> GetFieldsByIdAsync(IEnumerable<Guid> fieldIds)
    {
        var distinctIds = fieldIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, FieldDto>();
        }

        var fields = await _fieldAppService.GetListAsync(distinctIds);
        return fields.Items.ToDictionary(f => f.Id);
    }

    /// <summary>Translates <see cref="FieldFilters"/> into <paramref name="input"/>'s query filters - see that property's own remarks.</summary>
    protected virtual void ApplyFieldFilters(
        GetContentListInput input,
        IEnumerable<ContentTypeDto> contentTypes,
        IReadOnlyDictionary<Guid, FieldDto> fieldsById)
    {
        if (FieldFilters == null || FieldFilters.Count == 0)
        {
            return;
        }

        var fieldsByName = fieldsById.Values.ToDictionary(f => f.Name, StringComparer.Ordinal);

        var searchableNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var usage in contentTypes.SelectMany(ct => ct.Fields))
        {
            if (usage.Searchable && fieldsById.TryGetValue(usage.FieldId, out var field))
            {
                searchableNames.Add(field.Name);
            }
        }

        List<FlexFieldQueryCondition>? conditions = null;

        foreach (var (key, capturedValue) in FieldFilters)
        {
            // A placeholder's key is its name alone, or name:FORMAT when a format is present - never its
            // regex (PageRoute.cs's own Accept/IsValid remarks) - so splitting on the first ':' always
            // recovers (name, format) correctly.
            var colonIndex = key.IndexOf(':');
            var name = colonIndex < 0 ? key : key[..colonIndex];
            var format = colonIndex < 0 ? null : key[(colonIndex + 1)..];

            if (!searchableNames.Contains(name) || !fieldsByName.TryGetValue(name, out var field))
            {
                continue; // unknown, or not opted into search (总体设计 §2.3 Searchable) - not filterable, ignore
            }

            var fieldConditions = BuildFieldConditions(field, format, capturedValue);
            if (fieldConditions == null)
            {
                continue;
            }

            (conditions ??= new List<FlexFieldQueryCondition>()).AddRange(fieldConditions);
        }

        if (conditions is { Count: > 0 })
        {
            input.FlexFieldConditions = conditions;
        }
    }

    /// <summary>
    /// Null when <paramref name="field"/>'s type is not indexable at all (e.g. rich text - 总体设计 §2.3),
    /// the only case with nothing to filter on. A DateTime-typed field carrying a FORMAT gets the same
    /// range treatment a <c>publishTime</c> route placeholder gets from <c>SiteRenderFilterValueMapper</c>,
    /// as two conditions (<see cref="FlexFieldQueryOperator.LessThan"/> is already exclusive, unlike
    /// <c>GetContentListInput.PublishedBefore</c> - no tick adjustment needed here); everything else is one
    /// <see cref="FlexFieldQueryOperator.Equals"/> condition against the raw captured text (the query
    /// executor parses <see cref="FlexFieldQueryCondition.Value"/> itself, per its own type - a caller
    /// building one never has to convert the value first).
    /// </summary>
    private IReadOnlyList<FlexFieldQueryCondition>? BuildFieldConditions(FieldDto field, string? format, string capturedValue)
    {
        var indexValueType = _fieldTypeResolver.Get(field.FieldTypeName).IndexValueType;
        if (indexValueType is not { } valueType)
        {
            return null;
        }

        if (valueType == FlexFieldValueType.DateTime && format != null &&
            RoutePlaceholderDateFormat.TryGetRange(format, capturedValue, out var start, out var endExclusive))
        {
            return new[]
            {
                new FlexFieldQueryCondition(
                    field.Id, field.Name, FlexFieldQueryOperator.GreaterThanOrEqual,
                    start.ToString("O", CultureInfo.InvariantCulture), FlexFieldValueType.DateTime),
                new FlexFieldQueryCondition(
                    field.Id, field.Name, FlexFieldQueryOperator.LessThan,
                    endExclusive.ToString("O", CultureInfo.InvariantCulture), FlexFieldValueType.DateTime)
            };
        }

        return new[]
        {
            new FlexFieldQueryCondition(field.Id, field.Name, FlexFieldQueryOperator.Equals, capturedValue, valueType)
        };
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
