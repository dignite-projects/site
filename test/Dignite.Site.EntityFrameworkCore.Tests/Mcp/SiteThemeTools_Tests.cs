using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dignite.Site.Admin.Themes;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Mcp.Themes;
using Dignite.Site.Themes;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Mcp;

public class SiteThemeTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteThemeTools _themeTools;
    private readonly FakeSiteThemePreviewRenderer _previewRenderer;

    public SiteThemeTools_Tests()
    {
        _themeTools = GetRequiredService<SiteThemeTools>();
        _previewRenderer = GetRequiredService<FakeSiteThemePreviewRenderer>();
    }

    private async Task<SiteReleaseDto> PublishAsync(string? comment = null) =>
        await _themeTools.PublishThemeAsync((await _themeTools.CheckThemeAsync()).Fingerprint, comment);

    [Fact]
    public async Task Should_List_The_Files_And_Settings()
    {
        var overview = await _themeTools.ListThemeFilesAsync();

        overview.DefaultLayout.ShouldBe("layouts/default");
        overview.LiveReleaseNumber.ShouldBeNull();
        overview.Files.Select(f => (f.Path, f.IsAsset, f.Status)).ShouldBe(new[]
        {
            ("assets/site.css", true, "Added"),
            ("layouts/default", false, "Added")
        });
    }

    [Fact]
    public async Task Should_Save_Read_And_Report_Syntax_Errors()
    {
        var saved = await _themeTools.SaveThemeFileAsync("blog/post", "<p>\n{{ if x }}");

        saved.Path.ShouldBe("blog/post");
        saved.SyntaxErrors.ShouldNotBeEmpty();
        saved.SyntaxErrors[0].ShouldStartWith("(2,");

        var read = await _themeTools.GetThemeFileAsync("blog/post");
        read.Body.ShouldBe("<p>\n{{ if x }}");
        read.SyntaxErrors.ShouldNotBeEmpty();
        read.Release.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Read_A_File_As_A_Release_Has_It()
    {
        await _themeTools.SaveThemeFileAsync("Default", "<p>one</p>");
        await PublishAsync();
        await _themeTools.SaveThemeFileAsync("Default", "<p>two</p>");

        var read = await _themeTools.GetThemeFileAsync("Default", release: 1);

        read.Body.ShouldBe("<p>one</p>");
        read.Release.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Publish_List_And_Roll_Back_By_Number()
    {
        await _themeTools.SaveThemeFileAsync("Default", "<p>one</p>");
        (await PublishAsync("first")).Number.ShouldBe(1);
        await _themeTools.SaveThemeFileAsync("Default", "<p>two</p>");
        await PublishAsync("second");

        var rolledBack = await _themeTools.SetLiveReleaseAsync(1);

        rolledBack.Number.ShouldBe(1);
        rolledBack.IsLive.ShouldBeTrue();
        (await _themeTools.ListReleasesAsync()).Select(r => (r.Number, r.IsLive)).ShouldBe(new[] { (2, false), (1, true) });
        (await _themeTools.ListThemeFilesAsync()).LiveReleaseNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Throw_Not_Found_For_An_Unknown_Release_Number()
    {
        await Should.ThrowAsync<McpEntityNotFoundException>(() => _themeTools.SetLiveReleaseAsync(9));
    }

    [Fact]
    public async Task Should_Restore_A_Release_Into_The_Working_Copy()
    {
        await _themeTools.SaveThemeFileAsync("Default", "<p>one</p>");
        await PublishAsync();
        await _themeTools.SaveThemeFileAsync("Default", "<p>two</p>");

        await _themeTools.RestoreReleaseAsync(1);

        (await _themeTools.GetThemeFileAsync("Default")).Body.ShouldBe("<p>one</p>");
    }

    [Fact]
    public async Task Should_Report_What_Used_A_Deleted_File()
    {
        await _themeTools.SaveThemeFileAsync("Default", "{{ include \"_card\" }}");
        await _themeTools.SaveThemeFileAsync("_card", "");

        var usages = await _themeTools.DeleteThemeFileAsync("_card");

        usages.Usages.ShouldHaveSingleItem().SourcePath.ShouldBe("Default");
        (await _themeTools.CheckThemeAsync()).Issues.ShouldContain(i => i.Code == SiteThemeIssueCodes.ReferenceMissing);
    }

    [Fact]
    public async Task Should_Rename_Without_Sending_The_Body_Back()
    {
        await _themeTools.SaveThemeFileAsync("_card", "<div>card</div>");

        var result = await _themeTools.RenameThemeFileAsync("_card", "shared/_card");

        result.File.Path.ShouldBe("shared/_card");
        result.File.Body.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Set_The_Default_Layout()
    {
        await _themeTools.SaveThemeFileAsync("layouts/landing", "{{ site_head }}{{ body }}");

        (await _themeTools.UpdateThemeAsync("layouts/landing")).DefaultLayout.ShouldBe("layouts/landing");
        (await _themeTools.UpdateThemeAsync(string.Empty)).DefaultLayout.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Preview_The_Working_Copy_Or_A_Release()
    {
        await _themeTools.SaveThemeFileAsync("Default", "<p>one</p>");
        await PublishAsync();
        await _themeTools.SaveThemeFileAsync("Default", "<p>two</p>");

        var draft = await _themeTools.PreviewAsync("/about");
        var released = await _themeTools.PreviewAsync("/about", release: 1);

        draft.Html.ShouldBe("<html data-release=\"working-copy\"><p>two</p></html>");
        draft.Release.ShouldBeNull();
        draft.Layout.ShouldBe("layouts/default");
        draft.IsTruncated.ShouldBeFalse();
        draft.HtmlLength.ShouldBe(draft.Html.Length);
        released.Html.ShouldBe("<html data-release=\"1\"><p>one</p></html>");
        released.Release.ShouldBe(1);
        _previewRenderer.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Truncate_A_Long_Preview_And_Report_Its_Full_Length()
    {
        var body = new string('x', SiteThemeTools.MaxPreviewHtmlLength + 100);
        await _themeTools.SaveThemeFileAsync("Default", body);

        var preview = await _themeTools.PreviewAsync("/about");

        preview.IsTruncated.ShouldBeTrue();
        preview.Html.Length.ShouldBe(SiteThemeTools.MaxPreviewHtmlLength);
        preview.HtmlLength.ShouldBeGreaterThan(SiteThemeTools.MaxPreviewHtmlLength);
    }

    /// <summary>
    /// A render failure arrives as the renderer's business exception, which the server's error envelope reports with
    /// its message - the template, line and column - so the tool lets it through.
    /// </summary>
    [Fact]
    public async Task Should_Let_A_Render_Failure_Propagate()
    {
        _previewRenderer.Failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewRenderFailed)
            .WithData("Name", "Default")
            .WithData("Message", "Default(1,4) : error : The variable or function `typo` was not found");

        var exception = await Should.ThrowAsync<BusinessException>(() => _themeTools.PreviewAsync("/about"));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewRenderFailed);
    }

    [Fact]
    public async Task Should_Refuse_A_Publish_With_Errors()
    {
        await _themeTools.SaveThemeFileAsync("layouts/default", "<body>{{ body }}</body>");

        var exception = await Should.ThrowAsync<BusinessException>(() => PublishAsync());

        exception.Code.ShouldBe(SiteErrorCodes.SiteThemeHasErrors);
    }

    /// <summary>The description states the cut-off as a literal, which has to follow the constant.</summary>
    [Fact]
    public void The_Preview_Description_Should_State_The_Truncation_Length()
    {
        typeof(SiteThemeTools)
            .GetMethod(nameof(SiteThemeTools.PreviewAsync))!
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description
            .ShouldContain(SiteThemeTools.MaxPreviewHtmlLength.ToString(CultureInfo.InvariantCulture));
    }
}
