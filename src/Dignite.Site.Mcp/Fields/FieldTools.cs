using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Site.Admin.Fields;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Fields;
using Dignite.Site.Mcp.Naming;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Fields;

/// <summary>
/// The tenant's field library: what a field <i>is</i>, independent of the content types that use it
/// (总体设计 §2.3).
/// <para>
/// There are no field-group tools. A group is "purely organizational, no runtime effect" - in a model's
/// tool list it would be noise (§6.2.3).
/// </para>
/// </summary>
[McpServerToolType]
public class FieldTools : ITransientDependency
{
    protected IFieldAdminAppService FieldAppService { get; }

    protected SiteMcpNameResolver NameResolver { get; }

    public FieldTools(IFieldAdminAppService fieldAppService, SiteMcpNameResolver nameResolver)
    {
        FieldAppService = fieldAppService;
        NameResolver = nameResolver;
    }

    [McpServerTool(Name = "list_fields", Title = "List field definitions", ReadOnly = true)]
    [Description(
        "Lists the tenant's whole field library, including fields no content type currently uses. Useful " +
        "before defining a content type: reuse an existing field rather than creating a near-duplicate, " +
        "since widening one definition then benefits every type that pulls it in.")]
    [Authorize(AdminPermissions.Fields.Default)]
    public virtual Task<ListResultDto<FieldDto>> ListFieldsAsync(
        [Description("Free-text filter over name and display name. Omit for all.")]
        string? filter = null)
    {
        return FieldAppService.GetListAsync(new GetFieldListInput { Filter = filter });
    }

    [McpServerTool(Name = "list_field_types", Title = "List available field types", ReadOnly = true)]
    [Description(
        "The field types a definition can be bound to, e.g. TextEdit or Select. Call this before " +
        "create_field if you are unsure what fieldTypeName to use.")]
    [Authorize(AdminPermissions.Fields.Default)]
    public virtual Task<ListResultDto<FieldTypeDto>> ListFieldTypesAsync()
    {
        return FieldAppService.GetFieldTypesAsync();
    }

    [McpServerTool(Name = "create_field", Title = "Create a field definition")]
    [Description(
        "Adds a definition to the field library. A field must exist before a content type can pull it " +
        "in. Definitions are shared across the whole site, so check list_fields first.")]
    [Authorize(AdminPermissions.Fields.Create)]
    public virtual Task<FieldDto> CreateFieldAsync(
        [Description(
            "Machine name, unique across the site, e.g. 'title'. THIS IS THE KEY VALUES ARE STORED " +
            "UNDER, so choose it carefully - changing it later is a data migration (rename_field).")]
        string name,
        [Description("Human-readable name, e.g. 'Title'.")]
        string displayName,
        [Description("The field type this field is bound to, e.g. 'TextEdit'. Call list_field_types for what is available.")]
        string fieldTypeName,
        [Description(
            "What belongs in this field, written for a future AI client to read while generating " +
            "content. Worth writing properly: alongside the content type's description, it is the whole " +
            "briefing a model gets.")]
        string? description = null,
        [Description("Type-specific configuration - length limits, the option list of a select, and so on. Shape depends on fieldTypeName.")]
        Dictionary<string, object?>? configuration = null)
    {
        return FieldAppService.CreateAsync(new CreateFieldDto
        {
            Name = name,
            DisplayName = displayName,
            FieldTypeName = fieldTypeName,
            Description = description,
            Configuration = configuration
        });
    }

    [McpServerTool(Name = "update_field", Title = "Update a field definition", Idempotent = true)]
    [Description(
        "Updates a field definition. Anything left null keeps its current value. The change applies " +
        "everywhere the field is used. Cannot rename - use rename_field.")]
    [Authorize(AdminPermissions.Fields.Update)]
    public virtual async Task<FieldDto> UpdateFieldAsync(
        [Description("The field's machine name.")]
        string field,
        [Description("New human-readable name. Omit to keep it.")]
        string? displayName = null,
        [Description("New field type. Omit to keep it. Changing this can invalidate values already stored.")]
        string? fieldTypeName = null,
        [Description("New description. Omit to keep it.")]
        string? description = null,
        [Description("The complete replacement configuration. Omit to leave it untouched.")]
        Dictionary<string, object?>? configuration = null)
    {
        var current = await NameResolver.GetFieldAsync(field);

        return await FieldAppService.UpdateAsync(current.Id, new UpdateFieldDto
        {
            DisplayName = displayName ?? current.DisplayName,
            FieldTypeName = fieldTypeName ?? current.FieldTypeName,
            Description = description ?? current.Description,
            Configuration = configuration ?? current.Configuration,
            GroupName = current.GroupName
        });
    }

    [McpServerTool(Name = "rename_field", Title = "Rename a field definition", Destructive = true)]
    [Description(
        "Renames a field definition AND moves every stored value to the new key across the whole site. " +
        "This is a data migration, not an edit - the field's name is the key its values are stored " +
        "under. Separate from update_field, and separately permissioned, for that reason. Any external " +
        "template or front end referring to the old name breaks. Confirm with the user first.")]
    [Authorize(AdminPermissions.Fields.Rename)]
    public virtual async Task<FieldDto> RenameFieldAsync(
        [Description("The field's current machine name.")] string field,
        [Description("The new machine name. Must be unique across the site.")] string newName)
    {
        var current = await NameResolver.GetFieldAsync(field);

        // Straight to RenameAsync. Never Update-then-rename and never SetName: FieldManager.RenameAsync
        // holds an ordering that keeps every stored value reachable, and stepping around it orphans them
        // all (总体设计 §2.4).
        return await FieldAppService.RenameAsync(current.Id, new RenameFieldDto { NewName = newName });
    }

    [McpServerTool(Name = "delete_field", Title = "Delete a field definition", Destructive = true)]
    [Description(
        "Deletes a field definition and strips its values from every content across the site. Content " +
        "types that still reference it are left as they are; the usage is simply skipped from then on. " +
        "Cannot be undone. Confirm with the user first.")]
    [Authorize(AdminPermissions.Fields.Delete)]
    public virtual async Task<string> DeleteFieldAsync(
        [Description("The field's machine name.")] string field)
    {
        var current = await NameResolver.GetFieldAsync(field);

        await FieldAppService.DeleteAsync(current.Id);

        return $"Deleted field '{field}' and its stored values across the site.";
    }
}
