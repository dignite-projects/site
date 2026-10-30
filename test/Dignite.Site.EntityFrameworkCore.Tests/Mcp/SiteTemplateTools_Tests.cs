using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dignite.Site.Admin.Templates;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Mcp.Templates;
using Dignite.Site.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Mcp;

public class SiteTemplateTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteTemplateTools _templateTools;
    private readonly FakeSiteTemplatePreviewRenderer _previewRenderer;

    public SiteTemplateTools_Tests()
    {
        _templateTools = GetRequiredService<SiteTemplateTools>();
        _previewRenderer = GetRequiredService<FakeSiteTemplatePreviewRenderer>();
    }

    [Fact]
    public async Task Should_Preview_The_Published_Version_By_Default()
    {
        var preview = await _templateTools.PreviewTemplateAsync(SiteTestData.PublishedTemplateName, "/about");

        preview.Name.ShouldBe(SiteTestData.PublishedTemplateName);
        preview.Version.ShouldBe(2);
        preview.IsPublished.ShouldBeTrue();
        preview.Path.ShouldBe("/about");
        preview.PageName.ShouldBe("about");
        preview.CultureName.ShouldBe("en");
        preview.PageTemplate.ShouldBe("Default");
        preview.Html.ShouldBe("<html data-template=\"Seeded/Published\" data-version=\"2\">v2 {{ page.name }}</html>");
        preview.IsTruncated.ShouldBeFalse();
        preview.HtmlLength.ShouldBe(preview.Html.Length);

        _previewRenderer.Requests.ShouldHaveSingleItem().Version.Number.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Preview_A_Staged_Version()
    {
        await _templateTools.SaveTemplateAsync(SiteTestData.PublishedTemplateName, "v3 {{ page.name }}", publish: false);

        var preview = await _templateTools.PreviewTemplateAsync(SiteTestData.PublishedTemplateName, "/about", version: 3);

        preview.Version.ShouldBe(3);
        preview.IsPublished.ShouldBeFalse();
        preview.Html.ShouldContain("v3 {{ page.name }}");
    }

    [Fact]
    public async Task Should_Truncate_A_Long_Preview_And_Report_Its_Full_Length()
    {
        var body = new string('x', SiteTemplateTools.MaxPreviewHtmlLength + 100);
        await _templateTools.SaveTemplateAsync(SiteTestData.PublishedTemplateName, body, publish: false);

        var preview = await _templateTools.PreviewTemplateAsync(SiteTestData.PublishedTemplateName, "/about", version: 3);

        var fullLength = "<html data-template=\"Seeded/Published\" data-version=\"3\"></html>".Length + body.Length;
        preview.IsTruncated.ShouldBeTrue();
        preview.HtmlLength.ShouldBe(fullLength);
        preview.Html.Length.ShouldBe(SiteTemplateTools.MaxPreviewHtmlLength);
    }

    /// <summary>
    /// A render failure arrives as the renderer's business exception, which the server's error envelope reports
    /// with its message - the template, line and column - so the tool lets it through.
    /// </summary>
    [Fact]
    public async Task Should_Let_A_Render_Failure_Propagate()
    {
        _previewRenderer.Failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewRenderFailed)
            .WithData("Name", SiteTestData.PublishedTemplateName)
            .WithData("Message", "Seeded/Published(1,4) : error : The variable or function `typo` was not found");

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _templateTools.PreviewTemplateAsync(SiteTestData.PublishedTemplateName, "/about"));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewRenderFailed);
        exception.Data["Name"].ShouldBe(SiteTestData.PublishedTemplateName);
    }

    /// <summary>A business exception - no page at that path - is reported in full by the server, so it propagates.</summary>
    [Fact]
    public async Task Should_Let_A_Business_Exception_Propagate()
    {
        _previewRenderer.Failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewPathNotFound).WithData("Path", "/nowhere");

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _templateTools.PreviewTemplateAsync(SiteTestData.PublishedTemplateName, "/nowhere"));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewPathNotFound);
    }

    [Fact]
    public async Task Should_Throw_Not_Found_When_Previewing_An_Unknown_Template()
    {
        await Should.ThrowAsync<McpEntityNotFoundException>(() => _templateTools.PreviewTemplateAsync("does/not-exist", "/about"));
    }

    /// <summary>The description states the cut-off as a literal, which has to follow the constant.</summary>
    [Fact]
    public void The_Preview_Description_Should_State_The_Truncation_Length()
    {
        typeof(SiteTemplateTools)
            .GetMethod(nameof(SiteTemplateTools.PreviewTemplateAsync))!
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description
            .ShouldContain(SiteTemplateTools.MaxPreviewHtmlLength.ToString(CultureInfo.InvariantCulture));
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
    public async Task Should_List_Only_Names_Under_The_Given_Prefix()
    {
        await _templateTools.SaveTemplateAsync("partials/mcp-card", "<b>card</b>");
        await _templateTools.SaveTemplateAsync("partials/mcp-footer", "<footer></footer>");

        var list = await _templateTools.ListTemplatesAsync(prefix: "partials/");

        list.Select(t => t.Name).ShouldBe(new[] { "partials/mcp-card", "partials/mcp-footer" });
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
