using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Mcp.Templates;
using Dignite.Site.Templates;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

public class SiteTemplateTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteTemplateTools _templateTools;

    public SiteTemplateTools_Tests()
    {
        _templateTools = GetRequiredService<SiteTemplateTools>();
    }

    [Fact]
    public async Task Should_Create_A_Template_On_First_Save_And_Show_It_In_The_List()
    {
        var saved = await _templateTools.SaveTemplateAsync("Mcp/New", "<p>{{ page.name }}</p>", comment: "first");

        saved.Name.ShouldBe("Mcp/New");
        saved.PublishedVersionNumber.ShouldBe(1);
        saved.LatestVersionNumber.ShouldBe(1);

        var list = await _templateTools.ListTemplatesAsync();
        list.Select(t => t.Name).ShouldContain("Mcp/New");
    }

    [Fact]
    public async Task Should_Append_A_Version_And_Publish_It_On_The_Second_Save()
    {
        await _templateTools.SaveTemplateAsync("Mcp/Twice", "one");

        var saved = await _templateTools.SaveTemplateAsync("Mcp/Twice", "two");

        saved.LatestVersionNumber.ShouldBe(2);
        saved.PublishedVersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Stage_A_Version_When_Publish_Is_False()
    {
        var saved = await _templateTools.SaveTemplateAsync(
            SiteTestData.PublishedTemplateName, "v3 {{ page.name }}", publish: false);

        saved.LatestVersionNumber.ShouldBe(3);
        saved.PublishedVersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Get_The_Published_Version_By_Default()
    {
        var detail = await _templateTools.GetTemplateAsync(SiteTestData.PublishedTemplateName);

        detail.Version.ShouldBe(2);
        detail.IsPublished.ShouldBeTrue();
        detail.Body.ShouldBe("v2 {{ page.name }}");
        detail.Versions.Select(v => v.Number).ShouldBe(new[] { 2, 1 });
        detail.Versions.Single(v => v.IsPublished).Number.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Get_A_Named_Version()
    {
        var detail = await _templateTools.GetTemplateAsync(SiteTestData.PublishedTemplateName, version: 1);

        detail.Version.ShouldBe(1);
        detail.IsPublished.ShouldBeFalse();
        detail.Body.ShouldBe("v1 {{ page.name }}");
    }

    [Fact]
    public async Task Should_Get_The_Latest_Version_Of_A_Template_That_Is_Not_Published()
    {
        var detail = await _templateTools.GetTemplateAsync(SiteTestData.DraftTemplateName);

        detail.PublishedVersionNumber.ShouldBeNull();
        detail.Version.ShouldBe(1);
        detail.IsPublished.ShouldBeFalse();
        detail.Body.ShouldBe("draft {{ page.name }}");
    }

    [Fact]
    public async Task Should_Roll_Back_By_Publishing_An_Older_Version()
    {
        var published = await _templateTools.PublishTemplateAsync(SiteTestData.PublishedTemplateName, 1);

        published.PublishedVersionNumber.ShouldBe(1);

        var detail = await _templateTools.GetTemplateAsync(SiteTestData.PublishedTemplateName);
        detail.Version.ShouldBe(1);
        detail.IsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Delete_A_Template()
    {
        var message = await _templateTools.DeleteTemplateAsync(SiteTestData.DraftTemplateName);

        message.ShouldContain("Razor");

        var list = await _templateTools.ListTemplatesAsync();
        list.Select(t => t.Name).ShouldNotContain(SiteTestData.DraftTemplateName);
    }

    [Fact]
    public async Task Should_Filter_The_List_By_Name()
    {
        var list = await _templateTools.ListTemplatesAsync(filter: "published");

        list.Select(t => t.Name).ShouldBe(new[] { SiteTestData.PublishedTemplateName });
    }

    [Fact]
    public async Task Should_Throw_Not_Found_For_An_Unknown_Template_Name()
    {
        await Should.ThrowAsync<McpEntityNotFoundException>(() => _templateTools.GetTemplateAsync("does/not-exist"));
        await Should.ThrowAsync<McpEntityNotFoundException>(() => _templateTools.PublishTemplateAsync("does/not-exist", 1));
        await Should.ThrowAsync<McpEntityNotFoundException>(() => _templateTools.DeleteTemplateAsync("does/not-exist"));
    }

    [Fact]
    public async Task Should_Surface_A_Syntax_Error_With_Its_Position()
    {
        var exception = await Should.ThrowAsync<SiteTemplateSyntaxException>(
            () => _templateTools.SaveTemplateAsync("Mcp/Broken", "line one\n{{ if page.name }} never closed"));

        exception.Errors.ShouldNotBeEmpty();
        exception.Errors.ShouldAllBe(error => error.Line >= 1 && error.Column >= 1);
    }
}
