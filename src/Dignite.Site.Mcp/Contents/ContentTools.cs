using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Admin.Fields;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Contents;
using Dignite.Site.Mcp.Naming;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using Volo.Abp.Validation;

namespace Dignite.Site.Mcp.Contents;

/// <summary>
/// The day-to-day operating surface: "post a news item, here is the body" (总体设计 §6.1).
/// <para>
/// Every method here is a name-to-id translation followed by a single application service call. It does
/// no validating of its own - <c>ContentManager</c> holds slug uniqueness, culture normalization,
/// page/type consistency and field validation, and duplicating any of it here is exactly how a second
/// implementation grows (§6.2.2). The one shaping it does do is the slug, for the reason in
/// <see cref="McpSlug"/>.
/// </para>
/// </summary>
[McpServerToolType]
public class ContentTools : ITransientDependency
{
    protected IContentAdminAppService ContentAppService { get; }

    protected SiteMcpNameResolver NameResolver { get; }

    protected IClock Clock { get; }

    /// <summary>Reads the SEO field's width limits for <see cref="CheckSeoWidthAsync"/>; null uses the defaults.</summary>
    protected IFieldAdminAppService? FieldAppService { get; }

    public ContentTools(
        IContentAdminAppService contentAppService,
        SiteMcpNameResolver nameResolver,
        IClock clock,
        IFieldAdminAppService? fieldAppService = null)
    {
        ContentAppService = contentAppService;
        NameResolver = nameResolver;
        Clock = clock;
        FieldAppService = fieldAppService;
    }

    [McpServerTool(Name = "site_list_contents", Title = "List contents", ReadOnly = true)]
    [Description(
        "Lists contents, newest first, including drafts. Every filter is optional; with none, it lists " +
        "across the whole site. Each row says where a content is - page, content type, language, slug, status, " +
        "publish time - and leaves its field values out, which is what a list of pages would otherwise " +
        "cost: read one with site_get_content, or pass includeFieldValues to get them all.")]
    [Authorize(SiteAdminPermissions.Contents.Default)]
    public virtual async Task<PagedResultDto<McpContentSummary>> ListContentsAsync(
        [Description("Page name to list under, from site_get_schema. Omit to list across the whole site.")]
        string? page = null,
        [Description("Content type name, from site_get_schema. Requires 'page', since a content type's name is unique only within its page.")]
        string? contentType = null,
        [Description("Language tag, from the schema's enabledLanguages. Omit for all languages.")]
        string? cultureName = null,
        [Description("Filter by publication state. Omit for both.")]
        ContentStatus? status = null,
        [Description("Free-text filter over the slug only - it does NOT search titles or body text. Omit unless you know part of the slug.")]
        string? filter = null,
        [Description("How many to skip, for paging.")]
        int skipCount = 0,
        [Description("How many to return. Keep this small unless you need more.")]
        int maxResultCount = 20,
        [Description("Include each content's field values. Off by default: with long text fields a page of results is large.")]
        bool includeFieldValues = false)
    {
        var pageDto = page.IsNullOrWhiteSpace() ? null : await NameResolver.GetPageAsync(page!);

        Guid? contentTypeId = null;
        if (!contentType.IsNullOrWhiteSpace())
        {
            // A validation error, not a not-found. Reporting "there is no content type named X" for a
            // type that does exist would send the model to site_get_schema, where it would find the name
            // listed and retry the identical call - the missing argument is `page`, and saying so is the
            // only message it can act on.
            if (pageDto == null)
            {
                throw new AbpValidationException(
                    "'contentType' cannot be used without 'page'.",
                    new List<ValidationResult>
                    {
                        new(
                            "A content type's name is unique only within its page, so filtering by one "
                            + "requires naming the page it belongs to. Supply 'page' as well, or drop "
                            + "'contentType' to list across the whole site.",
                            new[] { "page" })
                    });
            }

            contentTypeId = (await NameResolver.GetContentTypeAsync(pageDto, contentType!)).Id;
        }

        var result = await ContentAppService.GetListAsync(new GetContentListInput
        {
            PageId = pageDto?.Id,
            ContentTypeId = contentTypeId,
            CultureName = cultureName,
            Status = status,
            Filter = filter,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount
        });

        return new PagedResultDto<McpContentSummary>(
            result.TotalCount,
            result.Items.Select(content => ToSummary(content, includeFieldValues, storedFields: false)).ToList());
    }

    [McpServerTool(Name = "site_get_content", Title = "Get one content", ReadOnly = true)]
    [Description(
        "Reads one content by the triple that identifies it: page, language and slug. Returns its field " +
        "values keyed by field name, in the same shape site_update_content accepts.")]
    [Authorize(SiteAdminPermissions.Contents.Default)]
    public virtual async Task<ContentDto> GetContentAsync(
        [Description("Page name, from site_get_schema.")] string page,
        [Description("Language tag, from the schema's enabledLanguages.")] string cultureName,
        [Description("The content's slug. Pass an empty string for the page's own single content.")] string slug)
    {
        var (_, content) = await NameResolver.GetContentAsync(page, cultureName, slug);
        return content;
    }

    [McpServerTool(Name = "site_create_content", Title = "Create a content")]
    [Description(
        "Creates one content of the given type under the given page. Field values are validated against " +
        "the content type's field definitions; a value for a field the type does not declare is dropped, " +
        "and a missing required value is rejected with the field named. Returns where the content landed - page, " +
        "content type, language, slug, status, publish time - and storedFields, the names of the values kept: one " +
        "missing from it was dropped. The values are not echoed back; read them with site_get_content. warnings, " +
        "when present, says that the SEO title or description is wider than a search result shows (a Chinese or " +
        "Japanese character counts as two columns): the content is stored, but shorten it.")]
    [Authorize(SiteAdminPermissions.Contents.Create)]
    public virtual async Task<McpContentSummary> CreateContentAsync(
        [Description("Page name, from site_get_schema.")]
        string page,
        [Description("Content type name under that page, from site_get_schema. Pick the one whose description matches what is being written.")]
        string contentType,
        [Description("Language tag. Must be one of the schema's enabledLanguages - do not invent a variant.")]
        string cultureName,
        [Description(
            "The URL segment for this content, e.g. 'my-trip'. REQUIRED - state it explicitly. Whether an " +
            "empty string is accepted depends on the page's route (site_get_schema): a route with '{slug}' " +
            "requires a real slug from every content there; '{slug?}' also allows ONE content with an " +
            "empty slug, served at the page's own address - if that slot is already taken, an empty slug " +
            "fails with Site:040001, and if the route allows no slug at all, a non-empty one fails with " +
            "Site:040004. A route with '{slug:REGEX}' only accepts a slug that REGEX matches - any other " +
            "fails with Site:040006. A title is acceptable here and will be turned into a slug.")]
        string slug,
        [Description("Field values keyed by the field 'name' from site_get_schema. Not the display name.")]
        Dictionary<string, object?>? fieldValues = null,
        [Description("Published or Draft. Defaults to Draft.")]
        ContentStatus status = ContentStatus.Draft,
        [Description("When this content is considered published, ISO-8601. Defaults to now. A draft cannot carry a future time - publish with a future time to schedule instead.")]
        DateTime? publishTime = null)
    {
        var contentTypeDto = await NameResolver.GetContentTypeAsync(page, contentType);

        var created = await ContentAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = contentTypeDto.Id,
            // Passed through as given. Normalizing here as well as in ContentManager would be the second
            // normalization point 总体设计 §2.4 warns about; the tool's job is to make the model choose
            // from the schema's language list, which its description does.
            CultureName = cultureName,
            Slug = McpSlug.Normalize(slug),
            Status = status,
            PublishTime = publishTime ?? Clock.Now,
            FieldValues = fieldValues
        });

        var createdSummary = ToSummary(created, includeFieldValues: false, storedFields: true);
        createdSummary.Warnings = await CheckSeoWidthAsync(created);
        return createdSummary;
    }

    [McpServerTool(Name = "site_update_content", Title = "Update a content", Idempotent = true)]
    [Description(
        "Updates one content, addressed by page, language and slug. Anything left null is kept as it is - " +
        "so this can publish a draft, or reschedule, without resending the body. Supplying fieldValues " +
        "REPLACES the whole set: read the content first and send it back with your edits merged in, or " +
        "omit it entirely. Returns where the content is now, and storedFields, the names of its values; " +
        "the values are not echoed back. warnings, when present, says that the SEO title or description is wider " +
        "than a search result shows (a Chinese or Japanese character counts as two columns): the content is " +
        "stored, but shorten it.")]
    [Authorize(SiteAdminPermissions.Contents.Update)]
    public virtual async Task<McpContentSummary> UpdateContentAsync(
        [Description("Page name, from site_get_schema.")]
        string page,
        [Description("Language tag of the content to update.")]
        string cultureName,
        [Description("The content's current slug. Pass an empty string for the page's own single content.")]
        string slug,
        [Description("A new slug, to move the content's URL. Omit to keep the current one.")]
        string? newSlug = null,
        [Description("The complete replacement set of field values, keyed by field name. Omit to leave every value untouched.")]
        Dictionary<string, object?>? fieldValues = null,
        [Description(
            "Published or Draft. Omit to keep the current state. Switching to Draft fails if the " +
            "publish time - the current one, or a new one given in the same call - is in the future: " +
            "a draft can never carry one. Pass a non-future publishTime alongside status:Draft to " +
            "un-schedule a content that was Published with a future publishTime.")]
        ContentStatus? status = null,
        [Description("New publish time, ISO-8601. Omit to keep the current one.")]
        DateTime? publishTime = null,
        [Description("Move the content to a different content type under the same page. Omit to keep it.")]
        string? contentType = null)
    {
        var (pageDto, content) = await NameResolver.GetContentAsync(page, cultureName, slug);

        Guid? contentTypeId = contentType.IsNullOrWhiteSpace()
            ? null
            : (await NameResolver.GetContentTypeAsync(pageDto, contentType!)).Id;

        var updated = await ContentAppService.UpdateAsync(content.Id, new UpdateContentDto
        {
            Slug = newSlug == null ? content.Slug : McpSlug.NormalizeReplacement(newSlug),
            Status = status ?? content.Status,
            PublishTime = publishTime ?? content.PublishTime,
            FieldValues = fieldValues,
            ContentTypeId = contentTypeId
        });

        var updatedSummary = ToSummary(updated, includeFieldValues: false, storedFields: true);
        updatedSummary.Warnings = await CheckSeoWidthAsync(updated);
        return updatedSummary;
    }

    /// <summary>
    /// What a client should know about the content it has just stored: an SEO title or description wider than the
    /// SEO field's limits (<see cref="SeoTextWidth"/>), which a search result would cut. Null when there is nothing to
    /// say. Advice only - the content is stored either way.
    /// </summary>
    protected virtual async Task<string[]?> CheckSeoWidthAsync(ContentDto content)
    {
        if (!content.FieldValues.TryGetValue(SeoTextWidth.SeoFieldName, out var raw) || raw == null)
        {
            return null;
        }

        var value = raw is JsonElement element ? element : JsonSerializer.SerializeToElement(raw, raw.GetType());
        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var (titleLimit, descriptionLimit) = await ReadSeoWidthLimitsAsync();
        var warnings = SeoTextWidth.Check(
            ReadString(value, "metaTitle"), ReadString(value, "metaDescription"), titleLimit, descriptionLimit);

        return warnings.Count == 0 ? null : warnings.ToArray();
    }

    /// <summary>
    /// The SEO field's limits as its configuration sets them. A client allowed to write contents but not to read the field
    /// library gets the defaults, as the admin UI would show them.
    /// </summary>
    protected virtual async Task<(int Title, int Description)> ReadSeoWidthLimitsAsync()
    {
        var title = SeoTextWidth.DefaultMetaTitleLimit;
        var description = SeoTextWidth.DefaultMetaDescriptionLimit;
        if (FieldAppService == null)
        {
            return (title, description);
        }

        try
        {
            var configuration = (await FieldAppService.FindByNameAsync(SeoTextWidth.SeoFieldName))?.Configuration;
            if (configuration != null)
            {
                title = ReadInt(configuration, SeoTextWidth.MetaTitleLimitKey) ?? title;
                description = ReadInt(configuration, SeoTextWidth.MetaDescriptionLimitKey) ?? description;
            }
        }
        catch (AbpAuthorizationException)
        {
            // The defaults, as above.
        }

        return (title, description);
    }

    private static string? ReadString(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static int? ReadInt(IDictionary<string, object?> configuration, string key)
    {
        if (!configuration.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var n) => n,
            JsonElement { ValueKind: JsonValueKind.String } text when int.TryParse(text.GetString(), out var n) => n,
            IConvertible convertible when int.TryParse(convertible.ToString(System.Globalization.CultureInfo.InvariantCulture), out var n) => n,
            _ => null
        };
    }

    /// <summary>
    /// A content as these tools say it back: where it is, by name, and either the names of its values or the values.
    /// <see cref="ContentDto.Url"/> is left out: the administration side leaves it empty, so it would only mislead.
    /// <c>site_resolve_path</c> gives the address a visitor reaches.
    /// </summary>
    protected virtual McpContentSummary ToSummary(ContentDto content, bool includeFieldValues, bool storedFields)
    {
        return new McpContentSummary
        {
            Page = content.PageName,
            ContentType = content.ContentTypeName,
            CultureName = content.CultureName,
            Slug = content.Slug,
            Status = content.Status,
            PublishTime = content.PublishTime,
            StoredFields = storedFields ? content.FieldValues.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray() : null,
            FieldValues = includeFieldValues ? content.FieldValues : null
        };
    }

    [McpServerTool(Name = "site_delete_content", Title = "Delete a content", Destructive = true)]
    [Description("Permanently deletes one content, addressed by page, language and slug. Other language versions of the same content are not affected.")]
    [Authorize(SiteAdminPermissions.Contents.Delete)]
    public virtual async Task<string> DeleteContentAsync(
        [Description("Page name, from site_get_schema.")] string page,
        [Description("Language tag of the content to delete.")] string cultureName,
        [Description("The content's slug. Pass an empty string for the page's own single content.")] string slug)
    {
        var (pageDto, content) = await NameResolver.GetContentAsync(page, cultureName, slug);

        await ContentAppService.DeleteAsync(content.Id);

        // Echoes the slug AND the page name that were actually resolved, not the strings the caller
        // passed. The slug can differ: the address falls back to a normalized form when the raw one
        // misses, so an identical retry after a successful delete resolves to a DIFFERENT content - the
        // normalized twin of the one just removed. Reporting the caller's own input back would make those
        // two deletions indistinguishable. The page name is the same idea applied to `pageDto.Name`
        // instead of the raw `page` argument, in case name resolution is ever case-insensitive.
        return content.Slug.Length == 0
            ? $"Deleted the '{content.CultureName}' content of page '{pageDto.Name}' (the page's own content)."
            : $"Deleted the '{content.CultureName}' content '{content.Slug}' under page '{pageDto.Name}'.";
    }
}
