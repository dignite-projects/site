using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Select;
using Dignite.Site.Admin.Fields;
using Dignite.Site.Admin.Schema;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.ContentTypes;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Mcp.Fields;
using Dignite.Site.Mcp.Pages;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// The site-building tools. The one that matters most here is <c>rename_field</c>: a field's name is the
/// key its values are stored under, so a rename is a data migration, and the tool has to go through the
/// manager that performs it (总体设计 §2.4).
/// </summary>
public class FieldTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly FieldTools _fieldTools;
    private readonly PageTools _pageTools;
    private readonly ContentTypeTools _contentTypeTools;
    private readonly IContentRepository _contentRepository;

    public FieldTools_Tests()
    {
        _fieldTools = GetRequiredService<FieldTools>();
        _pageTools = GetRequiredService<PageTools>();
        _contentTypeTools = GetRequiredService<ContentTypeTools>();
        _contentRepository = GetRequiredService<IContentRepository>();
    }

    /// <summary>
    /// The whole point of routing a rename through <c>FieldManager.RenameAsync</c>: the stored values move
    /// with the name. Calling <c>Field.SetName</c> instead would leave every one of them unreachable.
    /// <para>
    /// Checked across <b>every</b> content that carries the field, not just one. A migrator that only
    /// rewrites its first batch, or that scopes to a single page or culture, would move <c>my-trip</c> and
    /// silently strand the rest under a key nothing will ever read again - and since rename is documented
    /// as irreversible, the loss would be found by a reader rather than by a test.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Should_Move_Stored_Values_To_The_New_Key_For_Every_Content_That_Has_One()
    {
        await _fieldTools.RenameFieldAsync("title", "headline");

        // Spans three pages, two content types and two languages - including a draft, which the migration
        // must treat no differently from a published content.
        await AssertMovedAsync(SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug, "My trip");
        await AssertMovedAsync(SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.DraftSlug, "Draft post");
        await AssertMovedAsync(SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.GallerySlug, "Summer photos");
        await AssertMovedAsync(SiteTestData.HomePageId, SiteTestData.EnglishCulture, string.Empty, "Welcome");
        await AssertMovedAsync(SiteTestData.AboutPageId, SiteTestData.EnglishCulture, string.Empty, "About us");
        await AssertMovedAsync(SiteTestData.AboutPageId, SiteTestData.ChineseCulture, string.Empty, "关于我们");
    }

    private async Task AssertMovedAsync(Guid pageId, string cultureName, string slug, string expectedTitle)
    {
        var content = await _contentRepository.FindBySlugAsync(pageId, cultureName, slug);

        content.ShouldNotBeNull($"the seeded content '{slug}' ({cultureName}) should exist");
        JsonSerializer.Serialize(content!.GetField("headline"))
            .ShouldBe(JsonSerializer.Serialize(expectedTitle));
        content.GetField("title").ShouldBeNull($"'{slug}' ({cultureName}) still holds the old key");
    }

    /// <summary>
    /// Each of rename_field, list_fields and get_site_schema is its own MCP request - and therefore, under
    /// the Streamable HTTP transport this server uses (总体设计 §6.2.7), its own unit of work against the
    /// same database. Nothing here caches a field's name across that boundary, so a read immediately after
    /// the rename must see the new name and never the old one - both through the plain list and through
    /// the merged schema document that ContentTypeFieldResolver builds.
    /// </summary>
    [Fact]
    public async Task Should_Reflect_A_Rename_Immediately_In_List_And_Schema()
    {
        var renamed = await _fieldTools.RenameFieldAsync("title", "headline");
        renamed.Name.ShouldBe("headline");

        var listed = await _fieldTools.ListFieldsAsync();
        listed.Items.Select(field => field.Name).ShouldContain("headline");
        listed.Items.Select(field => field.Name).ShouldNotContain("title");

        var schema = await GetRequiredService<ISiteSchemaAdminAppService>().GetAsync();
        var namesInSchema = schema.Pages
            .SelectMany(page => page.ContentTypes)
            .SelectMany(contentType => contentType.Fields)
            .Select(field => field.Name)
            .ToList();

        namesInSchema.ShouldContain("headline");
        namesInSchema.ShouldNotContain("title");
    }

    [Fact]
    public async Task Should_Create_A_Field_And_Find_It_By_Name()
    {
        var created = await _fieldTools.CreateFieldAsync(
            name: "summary",
            displayName: "Summary",
            fieldTypeName: "TextEdit",
            description: "A one-paragraph summary, used as the meta description.");

        created.Name.ShouldBe("summary");

        var listed = await _fieldTools.ListFieldsAsync(filter: "summary");
        listed.Items.Select(field => field.Name).ShouldContain("summary");
    }

    [Fact]
    public async Task Should_Keep_Untouched_Properties_When_Updating_A_Field()
    {
        var updated = await _fieldTools.UpdateFieldAsync(
            field: "category",
            description: "Pick one or more topics for this post.");

        updated.Description.ShouldBe("Pick one or more topics for this post.");
        // Neither the display name nor the select's option list was supplied, so neither changed.
        updated.DisplayName.ShouldBe("Category");
        updated.FieldTypeName.ShouldBe("Select");
        updated.Configuration.ShouldContainKey(SelectConfigurationNames.Options);
    }

    /// <summary>
    /// There are no field-group tools (总体设计 §6.2.3) - group membership is not something an MCP client
    /// can see or name, so update_field has no <c>groupName</c> parameter and always resubmits the field's
    /// current group underneath. This pins that behaviour down: an edit to something unrelated must not
    /// have the side effect of clearing a field's group, which is what the naive "omit means keep" pattern
    /// used elsewhere in this method would do (omitted and "clear it" both arrive as null with no way to
    /// tell them apart).
    /// </summary>
    [Fact]
    public async Task Should_Keep_A_Fields_Group_Untouched_By_Update_Field()
    {
        var fieldService = GetRequiredService<IFieldAdminAppService>();
        var created = await fieldService.CreateAsync(new CreateFieldDto
        {
            Name = "grouped-field",
            DisplayName = "Grouped field",
            FieldTypeName = "TextEdit",
            GroupName = "seo"
        });
        created.GroupName.ShouldBe("seo");

        var updated = await _fieldTools.UpdateFieldAsync(field: "grouped-field", displayName: "Renamed display");

        updated.GroupName.ShouldBe("seo", "an unrelated edit must not clear or move the field's group");
    }

    [Fact]
    public async Task Should_Report_An_Unknown_Field_Name()
    {
        var exception = await Should.ThrowAsync<McpEntityNotFoundException>(async () =>
            await _fieldTools.UpdateFieldAsync(field: "titel", displayName: "Typo"));

        exception.EntityKind.ShouldBe("field");
        exception.Name.ShouldBe("titel");
    }

    /// <summary>
    /// The site-building sequence 总体设计 §6.1 describes, in one test: create the page, create the fields
    /// it needs, define the shape over them, then write a content into it - all by name, no Guid anywhere.
    /// </summary>
    [Fact]
    public async Task Should_Build_A_Section_From_Nothing_Addressing_Everything_By_Name()
    {
        await _pageTools.CreatePageAsync(
            name: "docs", displayName: "Docs", route: "/docs/{slug}");

        await _fieldTools.CreateFieldAsync(
            name: "doc-title", displayName: "Title", fieldTypeName: "TextEdit");

        await _fieldTools.CreateFieldAsync(
            name: "doc-body", displayName: "Body", fieldTypeName: "TextEdit");

        var contentType = await _contentTypeTools.CreateContentTypeAsync(
            page: "docs",
            name: "doc-page",
            displayName: "Doc page",
            description: "One page of the product documentation.",
            fields: new List<McpContentTypeFieldInput>
            {
                new() { Field = "doc-title", Required = true, Searchable = true, Order = 0 },
                new() { Field = "doc-body", Order = 1 }
            });

        contentType.Fields.Count.ShouldBe(2);

        var content = await GetRequiredService<Contents.ContentTools>().CreateContentAsync(
            page: "docs",
            contentType: "doc-page",
            cultureName: SiteTestData.EnglishCulture,
            slug: "getting-started",
            fieldValues: new Dictionary<string, object?>
            {
                ["doc-title"] = "Getting started",
                ["doc-body"] = "Install it, then run it."
            },
            status: ContentStatus.Published,
            publishTime: SiteTestData.PublishTime);

        content.Slug.ShouldBe("getting-started");
        JsonSerializer.Serialize(content.FieldValues["doc-title"]).ShouldBe("\"Getting started\"");
    }

    /// <summary>
    /// A content type may only pull in fields that already exist, and naming one that does not has to fail
    /// with that name rather than quietly producing a type with a hole in its arrangement.
    /// </summary>
    [Fact]
    public async Task Should_Refuse_A_Content_Type_Referencing_A_Field_That_Does_Not_Exist()
    {
        await _pageTools.CreatePageAsync(name: "faq", displayName: "FAQ", route: "/faq");

        var exception = await Should.ThrowAsync<McpEntityNotFoundException>(async () =>
            await _contentTypeTools.CreateContentTypeAsync(
                page: "faq",
                name: "faq-entry",
                displayName: "FAQ entry",
                fields: new List<McpContentTypeFieldInput>
                {
                    new() { Field = "question", Order = 0 }
                }));

        exception.EntityKind.ShouldBe("field");
        exception.Name.ShouldBe("question");
    }
}
