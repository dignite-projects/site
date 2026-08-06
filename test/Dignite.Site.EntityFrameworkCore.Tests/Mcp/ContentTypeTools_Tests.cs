using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Admin.Schema;
using Dignite.Site.Mcp.ContentTypes;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Site.Mcp;

public class ContentTypeTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ContentTypeTools _contentTypeTools;
    private readonly ISiteSchemaAdminAppService _schemaAppService;

    public ContentTypeTools_Tests()
    {
        _contentTypeTools = GetRequiredService<ContentTypeTools>();
        _schemaAppService = GetRequiredService<ISiteSchemaAdminAppService>();
    }

    /// <summary>
    /// update_content_type tells the caller to read the current arrangement from get_site_schema and send
    /// it back with changes merged in. Following that instruction must not destroy anything - so every
    /// usage flag the write side accepts has to survive the round trip, not just the ones a reader finds
    /// interesting. ShowInList and SchemaProperty were the two that did not.
    /// </summary>
    [Fact]
    public async Task Should_Round_Trip_Every_Usage_Flag_Through_The_Schema()
    {
        await _contentTypeTools.UpdateContentTypeAsync(
            page: "news",
            contentType: "news-item",
            fields: new List<McpContentTypeFieldInput>
            {
                new()
                {
                    Field = "title",
                    Required = true,
                    Searchable = true,
                    ShowInList = true,
                    Order = 0,
                    SchemaProperty = "headline"
                }
            });

        // Read it back the way the tool's own description tells a client to.
        var schema = await _schemaAppService.GetAsync();
        var read = schema.Pages.Single(page => page.Name == "news")
            .ContentTypes.Single().Fields.Single();

        read.ShowInList.ShouldBeTrue("ShowInList must be readable, or an edit silently clears it");
        read.SchemaProperty.ShouldBe("headline");

        // Now the merge the description prescribes: send back what was read, plus one more field.
        await _contentTypeTools.UpdateContentTypeAsync(
            page: "news",
            contentType: "news-item",
            fields: new List<McpContentTypeFieldInput>
            {
                new()
                {
                    Field = read.Name,
                    Required = read.Required,
                    Searchable = read.Searchable,
                    ShowInList = read.ShowInList,
                    Order = read.Order,
                    SchemaProperty = read.SchemaProperty
                },
                new() { Field = "body", Order = 1 }
            });

        var merged = (await _schemaAppService.GetAsync()).Pages.Single(page => page.Name == "news")
            .ContentTypes.Single().Fields.Single(field => field.Name == "title");

        merged.ShowInList.ShouldBeTrue("the documented merge workflow must not clear it");
        merged.SchemaProperty.ShouldBe("headline");
    }

    /// <summary>
    /// A duplicated field must be reported by name. The domain reports it as a bare FieldId - a Guid this
    /// name-addressed surface never shows the caller, so it could not map the error back to a field.
    /// </summary>
    [Fact]
    public async Task Should_Report_A_Duplicated_Field_By_Name_Not_By_Guid()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTypeTools.UpdateContentTypeAsync(
                page: "blog",
                contentType: "post-gallery",
                fields: new List<McpContentTypeFieldInput>
                {
                    new() { Field = "title", Order = 0 },
                    new() { Field = "title", Order = 1 }
                }));

        exception.ValidationErrors.Single().ErrorMessage.ShouldContain("title");
        exception.Message.ShouldContain("title");
    }

    /// <summary>
    /// An empty 'fields' list is a real value, not a stand-in for "omitted": update_content_type's own
    /// description says supplying 'fields' replaces the whole arrangement, and an empty list replaces it
    /// with nothing. Distinct from <see cref="Should_Leave_Fields_Untouched_When_The_Parameter_Is_Omitted"/>,
    /// which must NOT do this.
    /// </summary>
    [Fact]
    public async Task Should_Clear_The_Arrangement_When_Fields_Is_An_Empty_List()
    {
        await _contentTypeTools.UpdateContentTypeAsync(
            page: "news",
            contentType: "news-item",
            fields: new List<McpContentTypeFieldInput> { new() { Field = "title", Order = 0 } });

        (await _schemaAppService.GetAsync()).Pages.Single(page => page.Name == "news")
            .ContentTypes.Single().Fields.ShouldNotBeEmpty();

        await _contentTypeTools.UpdateContentTypeAsync(
            page: "news",
            contentType: "news-item",
            fields: new List<McpContentTypeFieldInput>());

        (await _schemaAppService.GetAsync()).Pages.Single(page => page.Name == "news")
            .ContentTypes.Single().Fields.ShouldBeEmpty();
    }

    /// <summary>
    /// The counterpart to <see cref="Should_Clear_The_Arrangement_When_Fields_Is_An_Empty_List"/>: leaving
    /// the parameter out entirely (its default, null) is the only way to keep the arrangement as it is.
    /// </summary>
    [Fact]
    public async Task Should_Leave_Fields_Untouched_When_The_Parameter_Is_Omitted()
    {
        await _contentTypeTools.UpdateContentTypeAsync(
            page: "news",
            contentType: "news-item",
            fields: new List<McpContentTypeFieldInput> { new() { Field = "title", Order = 0 } });

        // An edit to a different property, fields not mentioned at all.
        await _contentTypeTools.UpdateContentTypeAsync(
            page: "news",
            contentType: "news-item",
            displayName: "News Item (renamed)");

        var contentType = (await _schemaAppService.GetAsync()).Pages.Single(page => page.Name == "news")
            .ContentTypes.Single();

        contentType.DisplayName.ShouldBe("News Item (renamed)");
        contentType.Fields.Select(field => field.Name).ShouldBe(new[] { "title" });
    }

    /// <summary>
    /// A content type can legitimately have no fields yet (just created, or every field removed). The
    /// schema service resolves every content type's fields in one batch via
    /// <c>ContentTypeFieldResolver.ResolveManyAsync</c> and then indexes into the result by content type
    /// id - so a type with zero fields has to still produce a dictionary entry, or get_site_schema would
    /// throw for the one content type most likely to be mid-edit.
    /// </summary>
    [Fact]
    public async Task Should_Report_A_Content_Type_With_No_Fields_As_An_Empty_Arrangement()
    {
        await _contentTypeTools.CreateContentTypeAsync(
            page: "news",
            name: "news-empty",
            displayName: "Empty for now");

        var contentType = (await _schemaAppService.GetAsync()).Pages.Single(page => page.Name == "news")
            .ContentTypes.Single(ct => ct.Name == "news-empty");

        contentType.Fields.ShouldBeEmpty();
    }

    /// <summary>
    /// <c>ContentType.SetFields</c> sorts by Order with <c>Enumerable.OrderBy</c>, which .NET guarantees is
    /// stable - so two fields given the same Order are not left in an unspecified sequence a client could
    /// not predict; they keep the order they were listed in this call.
    /// </summary>
    [Fact]
    public async Task Should_Keep_Submission_Order_For_Fields_That_Share_The_Same_Order_Value()
    {
        await _contentTypeTools.UpdateContentTypeAsync(
            page: "news",
            contentType: "news-item",
            fields: new List<McpContentTypeFieldInput>
            {
                new() { Field = "body", Order = 0 },
                new() { Field = "title", Order = 0 }
            });

        var contentType = (await _schemaAppService.GetAsync()).Pages.Single(page => page.Name == "news")
            .ContentTypes.Single();

        contentType.Fields.Select(field => field.Name).ShouldBe(new[] { "body", "title" });
    }
}
