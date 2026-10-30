using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Seo;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// Turns a <see cref="RouteMatchDto"/> into the <see cref="SiteRenderViewModel"/> a template renders - shared by
/// <see cref="SiteRenderController"/>, which serves the page, and <c>ScribanTemplatePreviewRenderer</c>, which
/// renders the same page with a template version that is not published, so a preview sees exactly the model the
/// live page would.
/// </summary>
public static class SiteRenderViewModelBuilder
{
    /// <summary>
    /// <c>View()</c> resolves either form, but <c>Page.Template</c> is meant to hold a bare view name -
    /// stripping a ".cshtml" suffix here keeps that ergonomic without pretending to validate the value.
    /// Unlike before issue #53, a name that does not resolve to a real view is not this method's problem
    /// to catch - it throws the standard ASP.NET Core view-not-found error when <c>View()</c> renders it,
    /// same as a misconfigured Template always should have.
    /// </summary>
    public static string ResolveTemplateName(string template)
    {
        return template.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
            ? template[..^".cshtml".Length]
            : template;
    }

    /// <summary>
    /// The model for any match <see cref="SiteRenderController"/> renders: <see cref="BuildPage"/> for a
    /// <see cref="RouteMatchKindDto.Page"/>, <see cref="BuildContentAsync(RouteMatchDto, HeadMetadataDto?, IFieldPublicAppService)"/>
    /// for a <see cref="RouteMatchKindDto.ContentOfPage"/> or <see cref="RouteMatchKindDto.Content"/>; null for
    /// any other kind, or for a content match that has nothing to render.
    /// </summary>
    public static async Task<SiteRenderViewModel?> BuildAsync(
        RouteMatchDto match,
        HeadMetadataDto? headMetadata,
        IFieldPublicAppService fieldAppService)
    {
        return match.Kind switch
        {
            RouteMatchKindDto.Page => BuildPage(match, headMetadata),
            RouteMatchKindDto.ContentOfPage or RouteMatchKindDto.Content
                => await BuildContentAsync(match, headMetadata, fieldAppService),
            _ => null
        };
    }

    public static SiteRenderViewModel BuildPage(RouteMatchDto match, HeadMetadataDto? headMetadata)
    {
        var (fieldFilters, publishedAfter, publishedBefore) = SiteRenderFilterValueMapper.Build(match.FilterValues);
        return new SiteRenderViewModel
        {
            Page = match.Page!,
            CultureName = match.CultureName,
            FilterValues = new Dictionary<string, string>(match.FilterValues, StringComparer.OrdinalIgnoreCase),
            FieldFilters = fieldFilters,
            PublishedAfter = publishedAfter,
            PublishedBefore = publishedBefore,
            HeadMetadata = headMetadata
        };
    }

    public static Task<SiteRenderViewModel?> BuildContentAsync(
        RouteMatchDto match,
        HeadMetadataDto? headMetadata,
        IFieldPublicAppService fieldAppService)
    {
        return BuildContentAsync(match, headMetadata, fieldIds => GetFieldsByIdAsync(fieldIds, fieldAppService));
    }

    /// <summary>
    /// The content model, with the field definitions fetched by <paramref name="getFieldsByIdAsync"/> - so
    /// <see cref="SiteRenderController"/> keeps fetching them through its own overridable
    /// <c>GetFieldsByIdAsync</c>.
    /// </summary>
    public static async Task<SiteRenderViewModel?> BuildContentAsync(
        RouteMatchDto match,
        HeadMetadataDto? headMetadata,
        Func<IEnumerable<Guid>, Task<IReadOnlyDictionary<Guid, FieldDto>>> getFieldsByIdAsync)
    {
        if (match.Content == null || match.ContentType == null)
        {
            // RouteMatchDto.Content/.ContentType are genuinely nullable (e.g. the content's own content
            // type was deleted out from under it in a narrow admin-delete race) - nothing to render, not a
            // crash.
            return null;
        }

        var fieldsById = await getFieldsByIdAsync(match.ContentType.Fields.Select(f => f.FieldId));
        var (fieldFilters, publishedAfter, publishedBefore) = SiteRenderFilterValueMapper.Build(match.FilterValues);

        return new SiteRenderViewModel
        {
            Page = match.Page!,
            CultureName = match.CultureName,
            FilterValues = new Dictionary<string, string>(match.FilterValues, StringComparer.OrdinalIgnoreCase),
            FieldFilters = fieldFilters,
            PublishedAfter = publishedAfter,
            PublishedBefore = publishedBefore,
            HeadMetadata = headMetadata,
            Content = new ContentRenderViewModel
            {
                Content = match.Content,
                ContentType = match.ContentType,
                Fields = SiteRenderFieldMapper.Build(match.Content, match.ContentType, fieldsById, listFieldsOnly: false)
            }
        };
    }

    public static async Task<IReadOnlyDictionary<Guid, FieldDto>> GetFieldsByIdAsync(
        IEnumerable<Guid> fieldIds,
        IFieldPublicAppService fieldAppService)
    {
        var distinctIds = fieldIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, FieldDto>();
        }

        var fields = await fieldAppService.GetListAsync(distinctIds);
        return fields.Items.ToDictionary(f => f.Id);
    }
}
