using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.ContentTypes;
using Dignite.Site.Public.Contents;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Pages;
using Dignite.Site.Public.Routing;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// Queries one page's content list for a template - the querying half of <see cref="ContentListTagHelper"/>
/// (Razor templates), shared with the <c>contents</c> function of Scriban templates, so both engines list,
/// filter and page exactly alike. Rendering, and what to show when the page does not exist, stay with each
/// caller.
/// </summary>
public class ContentListQuery : ITransientDependency
{
    protected IContentPublicAppService ContentAppService { get; }
    protected IContentTypePublicAppService ContentTypeAppService { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected IPagePublicAppService PageAppService { get; }
    protected IFieldTypeResolver FieldTypeResolver { get; }

    public ContentListQuery(
        IContentPublicAppService contentAppService,
        IContentTypePublicAppService contentTypeAppService,
        IFieldPublicAppService fieldAppService,
        IPagePublicAppService pageAppService,
        IFieldTypeResolver fieldTypeResolver)
    {
        ContentAppService = contentAppService;
        ContentTypeAppService = contentTypeAppService;
        FieldAppService = fieldAppService;
        PageAppService = pageAppService;
        FieldTypeResolver = fieldTypeResolver;
    }

    /// <summary>A page by its (tenant-unique) name, with its content types; null when there is none.</summary>
    public virtual Task<PageDto?> FindPageAsync(string pageName)
    {
        return PageAppService.FindByNameAsync(pageName);
    }

    public virtual async Task<ContentListRenderViewModel> QueryAsync(ContentListQueryRequest request)
    {
        var page = request.Page;

        // Non-null here means some earlier read (GetAsync/GetByRouteAsync/FindByNameAsync) already fetched
        // it - route resolution's own PageDto mapping leaves it null on purpose (总体设计 §2.3). A page found
        // by name always has it (FindByNameAsync uses includeDetails: true); only a route-resolved Page still
        // pays for this round trip.
        var contentTypes = page.ContentTypes ?? (await ContentTypeAppService.GetListByPageAsync(page.Id)).Items;
        var contentTypesById = contentTypes.ToDictionary(ct => ct.Id);

        var contentTypeId = request.ContentTypeId;
        if (contentTypeId == null && !string.IsNullOrEmpty(request.ContentTypeName))
        {
            var named = contentTypes.FirstOrDefault(ct => string.Equals(ct.Name, request.ContentTypeName, StringComparison.Ordinal));
            if (named == null)
            {
                // A content type of that name is not under this page (any more) - nothing of it to list, rather
                // than silently listing every type instead.
                return Empty(request);
            }

            contentTypeId = named.Id;
        }

        var fieldsById = await GetFieldsByIdAsync(
            contentTypesById.Values.SelectMany(ct => ct.Fields).Select(f => f.FieldId));

        var input = new GetContentListInput
        {
            PageId = page.Id,
            CultureName = request.CultureName,
            ContentTypeId = contentTypeId,
            Filter = request.Filter,
            PublishedAfter = request.PublishedAfter,
            PublishedBefore = request.PublishedBefore,
            MaxResultCount = request.MaxResultCount,
            SkipCount = (request.CurrentPage - 1) * request.MaxResultCount
        };

        ApplyFieldFilters(input, request.FieldFilters, contentTypesById.Values, fieldsById);

        var contents = await ContentAppService.GetListAsync(input);

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
            MaxResultCount = request.MaxResultCount,
            CurrentPage = request.CurrentPage
        };
    }

    protected virtual async Task<IReadOnlyDictionary<Guid, FieldDto>> GetFieldsByIdAsync(IEnumerable<Guid> fieldIds)
    {
        var distinctIds = fieldIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, FieldDto>();
        }

        var fields = await FieldAppService.GetListAsync(distinctIds);
        return fields.Items.ToDictionary(f => f.Id);
    }

    /// <summary>
    /// Translates <paramref name="fieldFilters"/> into <paramref name="input"/>'s query filters - see
    /// <see cref="ContentListQueryRequest.FieldFilters"/>.
    /// </summary>
    protected virtual void ApplyFieldFilters(
        GetContentListInput input,
        IReadOnlyDictionary<string, string>? fieldFilters,
        IEnumerable<ContentTypeDto> contentTypes,
        IReadOnlyDictionary<Guid, FieldDto> fieldsById)
    {
        if (fieldFilters == null || fieldFilters.Count == 0)
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

        // Every FORMAT-bearing part of one DateTime field, gathered before any of them is judged - see
        // BuildDateRangeConditions for why a part is never turned into a condition on its own.
        Dictionary<string, (FieldDto Field, List<(string Format, string CapturedValue)> Parts)>? dateParts = null;

        foreach (var (key, capturedValue) in fieldFilters)
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

            // Null when the field's type is not indexable at all (e.g. rich text - 总体设计 §2.3), the only
            // case with nothing to filter on.
            if (FieldTypeResolver.Get(field.FieldTypeName).IndexValueType is not { } valueType)
            {
                continue;
            }

            if (valueType == FlexFieldValueType.DateTime && format != null)
            {
                dateParts ??= new Dictionary<string, (FieldDto, List<(string, string)>)>(StringComparer.Ordinal);
                if (!dateParts.TryGetValue(field.Name, out var entry))
                {
                    entry = (field, new List<(string Format, string CapturedValue)>());
                    dateParts[field.Name] = entry;
                }
                entry.Parts.Add((format, capturedValue));
                continue;
            }

            // Everything else is one Equals condition against the raw captured text - the query executor
            // parses FlexFieldQueryCondition.Value itself, per its own type; a caller building one never
            // has to convert the value first.
            (conditions ??= new List<FlexFieldQueryCondition>()).Add(
                new FlexFieldQueryCondition(field.Id, field.Name, FlexFieldQueryOperator.Equals, capturedValue, valueType));
        }

        if (dateParts != null)
        {
            foreach (var (field, parts) in dateParts.Values)
            {
                if (BuildDateRangeConditions(field, parts) is { } range)
                {
                    (conditions ??= new List<FlexFieldQueryCondition>()).AddRange(range);
                }
            }
        }

        if (conditions is { Count: > 0 })
        {
            input.FlexFieldConditions = conditions;
        }
    }

    /// <summary>
    /// The same range treatment a <c>publishTime</c> route placeholder gets from
    /// <c>SiteRenderFilterValueMapper</c>, for a DateTime-typed field: every FORMAT-bearing part naming
    /// <paramref name="field"/> read together as one period - <c>{eventDate:yyyy}/{eventDate:MM}</c> is
    /// one month, not a year and a month judged separately (see <see cref="RoutePlaceholderDateFormat"/>)
    /// - as two conditions (<see cref="FlexFieldQueryOperator.LessThan"/> is already exclusive, unlike
    /// <c>GetContentListInput.PublishedBefore</c> - no tick adjustment needed here). Null when the parts do
    /// not form a complete period (a month with no year, or text that is not a date at all): that filter is
    /// dropped, the same way <c>SiteRenderFilterValueMapper</c> drops an unusable <c>publishTime</c>,
    /// rather than guessed at.
    /// </summary>
    private static IReadOnlyList<FlexFieldQueryCondition>? BuildDateRangeConditions(
        FieldDto field,
        List<(string Format, string CapturedValue)> parts)
    {
        if (!RoutePlaceholderDateFormat.TryGetRange(parts, out var start, out var endExclusive))
        {
            return null;
        }

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

    private static ContentListRenderViewModel Empty(ContentListQueryRequest request)
    {
        return new ContentListRenderViewModel
        {
            Items = Array.Empty<ContentRenderViewModel>(),
            TotalCount = 0,
            MaxResultCount = request.MaxResultCount,
            CurrentPage = request.CurrentPage
        };
    }
}

/// <summary>What <see cref="ContentListQuery.QueryAsync"/> lists.</summary>
public record ContentListQueryRequest
{
    /// <summary>The page to list content under.</summary>
    public required PageDto Page { get; init; }

    public string? CultureName { get; init; }

    /// <summary>Only contents of this content type. Takes priority over <see cref="ContentTypeName"/>.</summary>
    public Guid? ContentTypeId { get; init; }

    /// <summary>
    /// Only contents of the page's content type of this name; a name the page has no content type of lists
    /// nothing.
    /// </summary>
    public string? ContentTypeName { get; init; }

    public string? Filter { get; init; }

    /// <summary>
    /// Placeholders a partial page-route match resolved short of a slug (<c>RouteMatchDto.FilterValues</c>
    /// / <c>SiteRenderViewModel.FieldFilters</c>, 总体设计 §3.4), resolved purely against FlexFields - e.g.
    /// <c>{"category": "news"}</c> for a request matched against a page routed
    /// <c>/blog/{category}/{slug}</c>. Each entry names a business field, typed via its field type's own
    /// <c>IndexValueType</c> and gated on the content type having opted it into <c>Searchable</c>
    /// (总体设计 §2.3), and becomes one or more <see cref="FlexFieldQueryCondition"/>s - range-shaped when
    /// the field is itself DateTime-typed with a FORMAT, every such entry for one field read together as
    /// one period. An entry naming an unknown or non-searchable field, or date parts that do not form a
    /// complete period, is silently ignored, never an error - a route filter value is untrusted input and
    /// must never fail the whole render.
    /// <para>
    /// <c>PublishTime</c> is deliberately never a valid entry here - it is <c>Content</c>'s own system
    /// field, not a FlexFields field (总体设计 §2.4), and <c>SiteRenderFilterValueMapper</c> carves it out
    /// upstream before <c>SiteRenderViewModel.FieldFilters</c> is even built. Use
    /// <see cref="PublishedAfter"/>/<see cref="PublishedBefore"/> for that instead.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string>? FieldFilters { get; init; }

    /// <summary>Optional lower bound on <c>Content.PublishTime</c>, inclusive - passed straight through to <see cref="GetContentListInput.PublishedAfter"/>.</summary>
    public DateTime? PublishedAfter { get; init; }

    /// <summary>Optional upper bound on <c>Content.PublishTime</c>, inclusive - see <see cref="PublishedAfter"/>.</summary>
    public DateTime? PublishedBefore { get; init; }

    /// <summary>Number of returned results.</summary>
    public int MaxResultCount { get; init; } = 20;

    /// <summary>1-based page number.</summary>
    public int CurrentPage { get; init; } = 1;
}
