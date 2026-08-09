using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.ContentTypes;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.ContentTypes;
using Dignite.Site.Mcp.Naming;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace Dignite.Site.Mcp.ContentTypes;

/// <summary>
/// Defines the shapes of content a page carries - which fields from the library, and how this type uses
/// each of them (总体设计 §2.3).
/// <para>
/// A content type's <c>description</c> is not documentation, it is the mechanism by which "post a news
/// item" reaches <c>news-item</c> rather than <c>post-article</c>. The platform deliberately does not do
/// that mapping itself (§6.2.1), so the descriptions written through these tools are what a later
/// session's model reads to decide. The parameter descriptions here say so.
/// </para>
/// </summary>
[McpServerToolType]
public class ContentTypeTools : ITransientDependency
{
    protected IContentTypeAdminAppService ContentTypeAppService { get; }

    protected SiteMcpNameResolver NameResolver { get; }

    public ContentTypeTools(
        IContentTypeAdminAppService contentTypeAppService,
        SiteMcpNameResolver nameResolver)
    {
        ContentTypeAppService = contentTypeAppService;
        NameResolver = nameResolver;
    }

    [McpServerTool(Name = "create_content_type", Title = "Create a content type")]
    [Description(
        "Defines a shape of content under a page: an ordered arrangement of fields from the field " +
        "library. Every field named here must already exist - call create_field for any that does not. " +
        "The same field can be pulled into any number of content types, with different required and " +
        "searchable flags in each.")]
    [Authorize(AdminPermissions.ContentTypes.Create)]
    public virtual async Task<ContentTypeDto> CreateContentTypeAsync(
        [Description("The page this type belongs to, by name. A content type never moves to another page.")]
        string page,
        [Description("Machine name, unique within the page, e.g. 'post-article'. This is how other tools address it.")]
        string name,
        [Description("Human-readable name, e.g. 'Article'.")]
        string displayName,
        [Description(
            "What this type is for, written so a future AI client can tell it apart from its siblings. " +
            "This is what makes 'post a news item' land on the right type, so be concrete: say what kind " +
            "of content it holds and when to choose it over the others under this page.")]
        string? description = null,
        [Description("The fields this type pulls in, in order.")]
        List<McpContentTypeFieldInput>? fields = null)
    {
        var pageDto = await NameResolver.GetPageAsync(page);

        return await ContentTypeAppService.CreateAsync(new CreateContentTypeDto
        {
            PageId = pageDto.Id,
            Name = name,
            DisplayName = displayName,
            Description = description,
            Fields = await ResolveFieldsAsync(fields)
        });
    }

    [McpServerTool(Name = "update_content_type", Title = "Update a content type", Idempotent = true)]
    [Description(
        "Updates a content type. Anything left null keeps its current value. Supplying 'fields' REPLACES " +
        "the whole arrangement, so read the current one from get_site_schema and send it back with your " +
        "changes merged in - dropping a field from the arrangement stops its stored values being read " +
        "back, though it does not delete them. An empty 'fields' list is a real value, not a synonym for " +
        "omitting the parameter: it clears the arrangement down to zero fields. To leave fields alone, " +
        "do not pass the parameter at all.")]
    [Authorize(AdminPermissions.ContentTypes.Update)]
    public virtual async Task<ContentTypeDto> UpdateContentTypeAsync(
        [Description("The page this type belongs to, by name.")]
        string page,
        [Description("The type's current machine name.")]
        string contentType,
        [Description("A new machine name. Omit to keep the current one.")]
        string? name = null,
        [Description("New human-readable name. Omit to keep it.")]
        string? displayName = null,
        [Description("New description - see create_content_type on why this matters. Omit to keep it.")]
        string? description = null,
        [Description(
            "The complete replacement arrangement. Omit this parameter entirely to leave the fields " +
            "untouched. Passing an empty list is different from omitting it: it clears the arrangement to " +
            "zero fields, which is rarely what you want.")]
        List<McpContentTypeFieldInput>? fields = null)
    {
        var pageDto = await NameResolver.GetPageAsync(page);
        var current = await NameResolver.GetContentTypeAsync(pageDto, contentType);

        return await ContentTypeAppService.UpdateAsync(current.Id, new UpdateContentTypeDto
        {
            Name = name ?? current.Name,
            DisplayName = displayName ?? current.DisplayName,
            Description = description ?? current.Description,
            Fields = fields == null ? null : await ResolveFieldsAsync(fields)
        });
    }

    [McpServerTool(Name = "delete_content_type", Title = "Delete a content type", Destructive = true)]
    [Description(
        "Deletes a content type. Refused while any content still uses it - move or delete those contents " +
        "first. The field definitions it referenced are not affected; they live in the tenant-wide field " +
        "library and other types may still use them.")]
    [Authorize(AdminPermissions.ContentTypes.Delete)]
    public virtual async Task<string> DeleteContentTypeAsync(
        [Description("The page this type belongs to, by name.")] string page,
        [Description("The type's machine name.")] string contentType)
    {
        var pageDto = await NameResolver.GetPageAsync(page);
        var current = await NameResolver.GetContentTypeAsync(pageDto, contentType);

        await ContentTypeAppService.DeleteAsync(current.Id);

        return $"Deleted content type '{contentType}' from page '{page}'.";
    }

    /// <summary>
    /// Field name to <c>FieldId</c>, one lookup per entry. A name that does not exist fails the whole
    /// call with that name reported, rather than silently arranging a type with a hole in it.
    /// </summary>
    protected virtual async Task<List<ContentTypeFieldDto>?> ResolveFieldsAsync(
        List<McpContentTypeFieldInput>? fields)
    {
        if (fields == null)
        {
            return null;
        }

        // Caught here rather than left to the domain, which reports the duplicate as a bare FieldId. That
        // is a Guid the caller has never seen - this whole surface is name-addressed and the schema
        // document contains no ids - so it cannot map the error back to a field and cannot self-correct.
        // Listing the same field twice is a common merge slip for a model editing an arrangement.
        var duplicate = fields
            .GroupBy(field => field.Field, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate != null)
        {
            throw new AbpValidationException(
                $"The field '{duplicate.Key}' is listed more than once.",
                new List<ValidationResult>
                {
                    new(
                        $"A content type pulls in each field at most once - both entries would resolve to "
                        + $"the same key in every content's values. Keep one entry for '{duplicate.Key}'.",
                        new[] { "fields" })
                });
        }

        var resolved = new List<ContentTypeFieldDto>(fields.Count);

        foreach (var field in fields)
        {
            var definition = await NameResolver.GetFieldAsync(field.Field);

            resolved.Add(new ContentTypeFieldDto
            {
                FieldId = definition.Id,
                Required = field.Required,
                Searchable = field.Searchable,
                ShowInList = field.ShowInList,
                DisplayName = field.DisplayName,
                Order = field.Order
            });
        }

        return resolved;
    }
}
