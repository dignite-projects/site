using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Themes;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// site_get_theme_guide and the site://theme-guide resource are one document down two pipes, like the schema's pair
/// (<c>SiteSchemaTools_Tests</c>), and the document is the user documentation page, embedded - these tests are about
/// that promise: that the page really is in the assembly, that the two paths agree, and that the guide starts where a
/// model should start reading.
/// </summary>
public class SiteThemeGuide_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteThemeTools _themeTools;
    private readonly SiteThemeResources _themeResources;

    public SiteThemeGuide_Tests()
    {
        _themeTools = GetRequiredService<SiteThemeTools>();
        _themeResources = GetRequiredService<SiteThemeResources>();
    }

    [Fact]
    public async Task Should_Publish_The_Resource_As_The_Same_Document_The_Tool_Returns()
    {
        var fromTool = await _themeTools.GetThemeGuideAsync();

        fromTool.ShouldNotBeNullOrWhiteSpace();
        _themeResources.GetThemeGuide().ShouldBe(fromTool);
    }

    [Fact]
    public void Should_Start_At_The_Pages_Title_Not_At_The_Documentation_Sites_Search_Block()
    {
        SiteThemeGuide.Text.ShouldStartWith("# Scriban Templates");
        SiteThemeGuide.Text.ShouldNotContain("[doc-seo]");
    }

    /// <summary>
    /// The page is written inside a raw block so that the documentation site, which renders its pages with Scriban,
    /// does not read the examples' double braces as code. The guide is not rendered by anything: the block's markers
    /// would be noise in front of a model, and the examples must come through untouched.
    /// </summary>
    [Fact]
    public void Should_Leave_Out_The_Raw_Block_That_Protects_The_Page_On_The_Documentation_Site()
    {
        SiteThemeGuide.Text.ShouldNotContain("{%{");
        SiteThemeGuide.Text.ShouldNotContain("}%}");
        SiteThemeGuide.Text.ShouldEndWith("* [MCP Tools](../mcp/tools.md)");
        SiteThemeGuide.Text.ShouldContain("{{ include \"_card\" title: \"Hi\" }}");
    }

    /// <summary>
    /// The server refuses to start on a resource it cannot build, and that is not a failure a unit test of the method
    /// would see: the SDK reads the attribute and the signature. The URI is what a client asks for.
    /// </summary>
    [Fact]
    public void Should_Be_A_Resource_The_Sdk_Builds_At_The_Documented_Address()
    {
        var method = typeof(SiteThemeResources).GetMethod(nameof(SiteThemeResources.GetThemeGuide))!;

        var resource = McpServerResource.Create(method, _themeResources);

        resource.ProtocolResourceTemplate.UriTemplate.ShouldBe("site://theme-guide");
        resource.ProtocolResourceTemplate.MimeType.ShouldBe("text/markdown");
    }

    /// <summary>
    /// What a model cannot guess and the sandbox enforces: the variables and the functions. A rename of either section
    /// in the page would silently drop them from the guide, so the headings are pinned here.
    /// </summary>
    [Fact]
    public void Should_Carry_The_Variables_The_Functions_And_The_Limits_Of_A_Template()
    {
        SiteThemeGuide.Text.ShouldContain("## What a Template Sees");
        SiteThemeGuide.Text.ShouldContain("## Functions");
        SiteThemeGuide.Text.ShouldContain("## Limits");
        SiteThemeGuide.Text.ShouldContain("`site_head");
        SiteThemeGuide.Text.ShouldContain("`asset_url path`");
    }
}
