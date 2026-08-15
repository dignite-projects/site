using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Contents;
using Dignite.Site.Mcp.Naming;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.Application.Dtos;
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

    public ContentTools(
        IContentAdminAppService contentAppService,
        SiteMcpNameResolver nameResolver,
        IClock clock)
    {
        ContentAppService = contentAppService;
        NameResolver = nameResolver;
        Clock = clock;
    }

    [McpServerTool(Name = "list_contents", Title = "List contents", ReadOnly = true)]
    [Description(
        "Lists contents, newest first, including drafts. Every filter is optional; with none, it lists " +
        "across the whole site.")]
    [Authorize(SiteAdminPermissions.Contents.Default)]
    public virtual async Task<PagedResultDto<ContentDto>> ListContentsAsync(
        [Description("Page name to list under, from get_site_schema. Omit to list across the whole site.")]
        string? page = null,
        [Description("Content type name, from get_site_schema. Requires 'page', since a content type's name is unique only within its page.")]
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
        int maxResultCount = 20)
    {
        var pageDto = page.IsNullOrWhiteSpace() ? null : await NameResolver.GetPageAsync(page!);

        Guid? contentTypeId = null;
        if (!contentType.IsNullOrWhiteSpace())
        {
            // A validation error, not a not-found. Reporting "there is no content type named X" for a
            // type that does exist would send the model to get_site_schema, where it would find the name
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

        return await ContentAppService.GetListAsync(new GetContentListInput
        {
            PageId = pageDto?.Id,
            ContentTypeId = contentTypeId,
            CultureName = cultureName,
            Status = status,
            Filter = filter,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount
        });
    }

    [McpServerTool(Name = "get_content", Title = "Get one content", ReadOnly = true)]
    [Description(
        "Reads one content by the triple that identifies it: page, language and slug. Returns its field " +
        "values keyed by field name, in the same shape update_content accepts.")]
    [Authorize(SiteAdminPermissions.Contents.Default)]
    public virtual async Task<ContentDto> GetContentAsync(
        [Description("Page name, from get_site_schema.")] string page,
        [Description("Language tag, from the schema's enabledLanguages.")] string cultureName,
        [Description("The content's slug. Pass an empty string for the page's own single content.")] string slug)
    {
        var (_, content) = await NameResolver.GetContentAsync(page, cultureName, slug);
        return content;
    }

    [McpServerTool(Name = "create_content", Title = "Create a content")]
    [Description(
        "Creates one content of the given type under the given page. Field values are validated against " +
        "the content type's field definitions; a value for a field the type does not declare is dropped, " +
        "and a missing required value is rejected with the field named.")]
    [Authorize(SiteAdminPermissions.Contents.Create)]
    public virtual async Task<ContentDto> CreateContentAsync(
        [Description("Page name, from get_site_schema.")]
        string page,
        [Description("Content type name under that page, from get_site_schema. Pick the one whose description matches what is being written.")]
        string contentType,
        [Description("Language tag. Must be one of the schema's enabledLanguages - do not invent a variant.")]
        string cultureName,
        [Description(
            "The URL segment for this content, e.g. 'my-trip'. REQUIRED - state it explicitly. Whether an " +
            "empty string is accepted depends on the page's route (get_site_schema): a route with '{slug}' " +
            "requires a real slug from every content there; '{slug?}' also allows ONE content with an " +
            "empty slug, served at the page's own address - if that slot is already taken, an empty slug " +
            "fails with Site:040001, and if the route allows no slug at all, a non-empty one fails with " +
            "Site:040004. A title is acceptable here and will be turned into a slug.")]
        string slug,
        [Description("Field values keyed by the field 'name' from get_site_schema. Not the display name.")]
        Dictionary<string, object?>? fieldValues = null,
        [Description("Published or Draft. Defaults to Draft.")]
        ContentStatus status = ContentStatus.Draft,
        [Description("When this content is considered published, ISO-8601. Defaults to now. A draft cannot carry a future time - publish with a future time to schedule instead.")]
        DateTime? publishTime = null)
    {
        var contentTypeDto = await NameResolver.GetContentTypeAsync(page, contentType);

        return await ContentAppService.CreateAsync(new CreateContentDto
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
    }

    [McpServerTool(Name = "update_content", Title = "Update a content", Idempotent = true)]
    [Description(
        "Updates one content, addressed by page, language and slug. Anything left null is kept as it is - " +
        "so this can publish a draft, or reschedule, without resending the body. Supplying fieldValues " +
        "REPLACES the whole set: read the content first and send it back with your edits merged in, or " +
        "omit it entirely.")]
    [Authorize(SiteAdminPermissions.Contents.Update)]
    public virtual async Task<ContentDto> UpdateContentAsync(
        [Description("Page name, from get_site_schema.")]
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

        return await ContentAppService.UpdateAsync(content.Id, new UpdateContentDto
        {
            Slug = newSlug == null ? content.Slug : McpSlug.NormalizeReplacement(newSlug),
            Status = status ?? content.Status,
            PublishTime = publishTime ?? content.PublishTime,
            FieldValues = fieldValues,
            ContentTypeId = contentTypeId
        });
    }

    [McpServerTool(Name = "delete_content", Title = "Delete a content", Destructive = true)]
    [Description("Permanently deletes one content, addressed by page, language and slug. Other language versions of the same content are not affected.")]
    [Authorize(SiteAdminPermissions.Contents.Delete)]
    public virtual async Task<string> DeleteContentAsync(
        [Description("Page name, from get_site_schema.")] string page,
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
