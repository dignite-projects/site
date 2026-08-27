using System;
using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Admin.ContentTypes;
using Dignite.Site.Admin.Fields;
using Dignite.Site.Admin.Pages;
using Dignite.Site.Contents;
using Dignite.Site.ContentTypes;
using Dignite.Site.Mcp.Contents;
using Dignite.Site.Mcp.ContentTypes;
using Dignite.Site.Mcp.Fields;
using Dignite.Site.Mcp.Pages;
using Dignite.Site.Mcp.Routing;
using Dignite.Site.Public.Routing;

namespace Dignite.Site.Mcp;

/// <summary>How an MCP tool method fills one property of the DTO it constructs.</summary>
public enum McpPropertyMapping
{
    /// <summary>Filled from a parameter of the same camelCase name, whose type is (or is assignable to)
    /// the property's type - the ordinary pass-through.</summary>
    Direct,

    /// <summary>Filled from a parameter under a different name and/or a different shape - typically a
    /// human-readable name the tool resolves to a Guid via <c>SiteMcpNameResolver</c> (总体设计 §6.2.4),
    /// or a composite type it translates field-by-field (e.g. content-type field arrangements). The
    /// parameter must exist; its type is deliberately not compared against the property's, since the two
    /// are expected to differ.</summary>
    Translated,

    /// <summary>Never filled from any parameter - always left at its default, or copied from the current
    /// value on an update. <see cref="McpPropertyContract.Reason"/> is mandatory: this is the one state a
    /// structural check cannot tell apart from a bug by construction (an omission and an oversight look
    /// identical in the code), so the reason is the only thing that can - see the discussion in
    /// <see cref="McpToolDtoContract_Tests"/>.</summary>
    Omitted,
}

/// <summary>One property on a Create/Update/List DTO, and how (if at all) the owning tool exposes it.</summary>
public sealed record McpPropertyContract(
    string Property,
    Type ExpectedType,
    McpPropertyMapping Mapping,
    string? Parameter = null,
    string? Reason = null);

/// <summary>One MCP tool method and the input DTO it builds from its parameters.</summary>
public sealed record McpToolContract(Type ToolsType, string MethodName, Type DtoType, McpPropertyContract[] Properties)
{
    public override string ToString() => $"{ToolsType.Name}.{MethodName} -> {DtoType.Name}";
}

/// <summary>
/// The declared shape of every MCP tool that builds a Create/Update/List DTO - see
/// <see cref="McpToolDtoContract_Tests"/> for what this is checked against and why it exists at all.
/// <para>
/// Adding a property to one of these DTOs on the API side means adding it here too, categorized as
/// <see cref="McpPropertyMapping.Direct"/>, <see cref="McpPropertyMapping.Translated"/> or
/// <see cref="McpPropertyMapping.Omitted"/> (with a reason) - the test fails until that decision is made,
/// which is the point: silence is exactly what let Page.Template/ContentTemplate go unreachable from MCP
/// with nothing to say so.
/// </para>
/// </summary>
public static class McpToolContracts
{
    private static McpPropertyContract Direct(string property, Type type, string parameter) =>
        new(property, type, McpPropertyMapping.Direct, Parameter: parameter);

    private static McpPropertyContract Translated(string property, Type type, string parameter) =>
        new(property, type, McpPropertyMapping.Translated, Parameter: parameter);

    private static McpPropertyContract Omitted(string property, Type type, string reason) =>
        new(property, type, McpPropertyMapping.Omitted, Reason: reason);

    public static readonly McpToolContract[] All =
    {
        new(typeof(PageTools), nameof(PageTools.CreatePageAsync), typeof(CreatePageDto),
        [
            Direct("Name", typeof(string), "name"),
            Direct("DisplayName", typeof(string), "displayName"),
            Direct("Route", typeof(string), "route"),
            Direct("Template", typeof(string), "template"),
            Direct("ContentTemplate", typeof(string), "contentTemplate"),
            Translated("ParentId", typeof(Guid?), "parent"),
            Direct("IsActive", typeof(bool), "isActive"),
        ]),

        new(typeof(PageTools), nameof(PageTools.UpdatePageAsync), typeof(UpdatePageDto),
        [
            Direct("Name", typeof(string), "name"),
            Direct("DisplayName", typeof(string), "displayName"),
            Direct("Route", typeof(string), "route"),
            Direct("Template", typeof(string), "template"),
            Direct("ContentTemplate", typeof(string), "contentTemplate"),
            Translated("ParentId", typeof(Guid?), "parent"),
            Direct("IsActive", typeof(bool), "isActive"),
        ]),

        new(typeof(ContentTypeTools), nameof(ContentTypeTools.CreateContentTypeAsync), typeof(CreateContentTypeDto),
        [
            Translated("PageId", typeof(Guid), "page"),
            Direct("Name", typeof(string), "name"),
            Direct("DisplayName", typeof(string), "displayName"),
            Direct("Description", typeof(string), "description"),
            Translated("Fields", typeof(List<ContentTypeFieldDto>), "fields"),
        ]),

        new(typeof(ContentTypeTools), nameof(ContentTypeTools.UpdateContentTypeAsync), typeof(UpdateContentTypeDto),
        [
            Direct("Name", typeof(string), "name"),
            Direct("DisplayName", typeof(string), "displayName"),
            Direct("Description", typeof(string), "description"),
            Translated("Fields", typeof(List<ContentTypeFieldDto>), "fields"),
        ]),

        new(typeof(ContentTools), nameof(ContentTools.CreateContentAsync), typeof(CreateContentDto),
        [
            Translated("ContentTypeId", typeof(Guid), "contentType"),
            Direct("CultureName", typeof(string), "cultureName"),
            Direct("Slug", typeof(string), "slug"),
            Direct("PublishTime", typeof(DateTime), "publishTime"),
            Direct("Status", typeof(ContentStatus), "status"),
            Direct("FieldValues", typeof(IDictionary<string, object?>), "fieldValues"),
        ]),

        new(typeof(ContentTools), nameof(ContentTools.UpdateContentAsync), typeof(UpdateContentDto),
        [
            Translated("Slug", typeof(string), "newSlug"),
            Direct("PublishTime", typeof(DateTime), "publishTime"),
            Direct("Status", typeof(ContentStatus), "status"),
            Direct("FieldValues", typeof(IDictionary<string, object?>), "fieldValues"),
            Translated("ContentTypeId", typeof(Guid?), "contentType"),
        ]),

        new(typeof(ContentTools), nameof(ContentTools.ListContentsAsync), typeof(GetContentListInput),
        [
            Translated("PageId", typeof(Guid?), "page"),
            Direct("CultureName", typeof(string), "cultureName"),
            Translated("ContentTypeId", typeof(Guid?), "contentType"),
            Direct("Status", typeof(ContentStatus?), "status"),
            Omitted("PublishedBefore", typeof(DateTime?),
                "Open gap, not a design decision - flagged 2026-08-27 alongside the Template/ContentTemplate " +
                "fix this test suite was built for, but left unwired pending a decision on whether to expose " +
                "it. Revisit before relying on list_contents for time-range queries."),
            Omitted("PublishedAfter", typeof(DateTime?),
                "Same open gap as PublishedBefore - see there."),
            Direct("Filter", typeof(string), "filter"),
            Omitted("FlexFieldConditions", typeof(List<FlexFieldQueryCondition>),
                "Structured per-field query conditions - deliberately not exposed as a raw MCP parameter; " +
                "there is no ergonomic shape for a model to construct one. Revisit if content search needs " +
                "grow past the slug/status/date filters list_contents already has."),
            Omitted("Sorting", typeof(string),
                "list_contents deliberately hardcodes newest-first (see its [Description]) rather than " +
                "exposing a raw sort-field string, which would leak internal property names to the model."),
            Direct("SkipCount", typeof(int), "skipCount"),
            Direct("MaxResultCount", typeof(int), "maxResultCount"),
        ]),

        new(typeof(FieldTools), nameof(FieldTools.CreateFieldAsync), typeof(CreateFieldDto),
        [
            Direct("Name", typeof(string), "name"),
            Direct("DisplayName", typeof(string), "displayName"),
            Direct("FieldTypeName", typeof(string), "fieldTypeName"),
            Direct("Description", typeof(string), "description"),
            Direct("Configuration", typeof(IDictionary<string, object?>), "configuration"),
            Omitted("GroupName", typeof(string),
                "Deliberate - no field-group tools at all. A group is purely organizational with no " +
                "runtime effect (总体设计 §2.3), so it would be pure noise in an AI client's tool list " +
                "(总体设计 §6.2.3; see the class doc on FieldTools)."),
        ]),

        new(typeof(FieldTools), nameof(FieldTools.UpdateFieldAsync), typeof(UpdateFieldDto),
        [
            Direct("DisplayName", typeof(string), "displayName"),
            Direct("FieldTypeName", typeof(string), "fieldTypeName"),
            Direct("Description", typeof(string), "description"),
            Direct("Configuration", typeof(IDictionary<string, object?>), "configuration"),
            Omitted("GroupName", typeof(string), "Deliberate - same reason as CreateFieldDto.GroupName."),
        ]),

        new(typeof(FieldTools), nameof(FieldTools.RenameFieldAsync), typeof(RenameFieldDto),
        [
            Direct("NewName", typeof(string), "newName"),
        ]),

        new(typeof(FieldTools), nameof(FieldTools.ListFieldsAsync), typeof(GetFieldListInput),
        [
            Direct("Filter", typeof(string), "filter"),
        ]),

        new(typeof(RoutingTools), nameof(RoutingTools.ResolvePathAsync), typeof(ResolvePathInput),
        [
            Direct("Path", typeof(string), "path"),
        ]),
    };
}
