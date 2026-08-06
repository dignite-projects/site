using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Site.Admin.Schema;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Schema;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// get_site_schema and the site://schema resource are meant to be the same document down two different
/// pipes (总体设计 §6.2.4) - "there is no second answer to keep in step", per SiteSchemaResources's own
/// doc comment. These tests are about that promise specifically: that the tool and the resource actually
/// agree, and that the resource's wire format is what an MCP client expects. The document's own content
/// (which fields survive, how usage merges with definition, and so on) is already covered in depth by
/// <c>SiteSchemaAdminAppService_Tests</c>, which both of these thin wrappers delegate to.
/// </summary>
public class SiteSchemaTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly SiteSchemaTools _schemaTools;
    private readonly SiteSchemaResources _schemaResources;

    public SiteSchemaTools_Tests()
    {
        _schemaTools = GetRequiredService<SiteSchemaTools>();
        _schemaResources = GetRequiredService<SiteSchemaResources>();
    }

    [Fact]
    public async Task Should_Return_The_Schema_Document()
    {
        var schema = await _schemaTools.GetSiteSchemaAsync();

        schema.Pages.ShouldNotBeEmpty();
        schema.Pages.Single(page => page.Name == "blog").ContentTypes.ShouldNotBeEmpty();
    }

    /// <summary>
    /// The resource is documented as "the same document get_site_schema returns", not merely an
    /// equivalent one - so the values, not just the shape, have to match between the two paths.
    /// </summary>
    [Fact]
    public async Task Should_Publish_The_Resource_As_The_Same_Document_The_Tool_Returns()
    {
        var fromTool = await _schemaTools.GetSiteSchemaAsync();
        var resourceJson = await _schemaResources.GetSiteSchemaAsync();

        var fromResource = JsonSerializer.Deserialize<SiteSchemaDto>(resourceJson, WebOptions);

        fromResource.ShouldNotBeNull();
        fromResource!.EnabledLanguages.ShouldBe(fromTool.EnabledLanguages);
        fromResource.DefaultLanguage.ShouldBe(fromTool.DefaultLanguage);
        fromResource.PrimaryDomain.ShouldBe(fromTool.PrimaryDomain);
        fromResource.Pages.Select(page => page.Name)
            .ShouldBe(fromTool.Pages.Select(page => page.Name), ignoreOrder: true);

        var toolBlog = fromTool.Pages.Single(page => page.Name == "blog");
        var resourceBlog = fromResource.Pages.Single(page => page.Name == "blog");
        resourceBlog.ContentTypes.Select(contentType => contentType.Name)
            .ShouldBe(toolBlog.ContentTypes.Select(contentType => contentType.Name), ignoreOrder: true);
    }

    /// <summary>
    /// The resource is consumed as raw JSON by a client, never round-tripped back through the C# type, so
    /// what actually matters is the wire casing - not merely that <c>Deserialize</c> can tolerate it back.
    /// MCP/JSON-RPC convention is camelCase, and SiteSchemaResources sets JsonSerializerDefaults.Web to
    /// match; this pins the property names a client actually receives rather than trusting that choice by
    /// inspection alone.
    /// </summary>
    [Fact]
    public async Task Should_Serialize_The_Resource_With_CamelCase_Property_Names()
    {
        var json = await _schemaResources.GetSiteSchemaAsync();

        using var document = JsonDocument.Parse(json);

        document.RootElement.TryGetProperty("enabledLanguages", out _).ShouldBeTrue();
        document.RootElement.TryGetProperty("defaultLanguage", out _).ShouldBeTrue();
        document.RootElement.TryGetProperty("primaryDomain", out _).ShouldBeTrue();
        document.RootElement.TryGetProperty("pages", out _).ShouldBeTrue();

        // Case-sensitive by design (JsonElement.TryGetProperty does not fold case) - this is what would
        // catch a regression to the default PascalCase writer, which fromResource above would not.
        document.RootElement.TryGetProperty("EnabledLanguages", out _).ShouldBeFalse();
    }
}
