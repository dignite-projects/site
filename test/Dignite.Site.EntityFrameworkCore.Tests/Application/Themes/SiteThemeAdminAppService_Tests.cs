using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Themes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Localization;
using Xunit;

namespace Dignite.Site.Admin.Themes;

public class SiteThemeAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string Layout = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>";

    private readonly ISiteThemeAdminAppService _themeAppService;
    private readonly FakeSiteThemePreviewRenderer _previewRenderer;

    public SiteThemeAdminAppService_Tests()
    {
        _themeAppService = GetRequiredService<ISiteThemeAdminAppService>();
        _previewRenderer = GetRequiredService<FakeSiteThemePreviewRenderer>();
    }

    private Task<SiteThemeFileDto> SaveAsync(string path, string body) =>
        _themeAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = path, Body = body });

    private async Task<SiteReleaseDto> PublishAsync(string? comment = null) =>
        await _themeAppService.PublishAsync(new PublishSiteThemeDto
        {
            Fingerprint = (await _themeAppService.CheckAsync()).Fingerprint,
            Comment = comment
        });

    [Fact]
    public async Task Should_Start_A_Theme_With_The_Default_Layout_And_Nothing_Live()
    {
        var theme = await _themeAppService.GetAsync();

        theme.DefaultLayout.ShouldBe("layouts/default");
        theme.LiveReleaseId.ShouldBeNull();
        theme.LiveReleaseNumber.ShouldBeNull();
        theme.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_List_Files_With_Their_Status_Against_The_Live_Release()
    {
        (await _themeAppService.GetFilesAsync()).Items.ShouldAllBe(f => f.Status == SiteThemeFileStatus.Added);

        await SaveAsync("Events", "<p>x</p>");
        await PublishAsync();
        await SaveAsync("Events", "<p>y</p>");
        await SaveAsync("_new", "");
        await _themeAppService.DeleteFileAsync("assets/site.css");

        var files = (await _themeAppService.GetFilesAsync()).Items.ToDictionary(f => f.Path);

        files.Keys.ShouldBe(new[] { "Events", "_new", "assets/site.css", "layouts/default" });
        files["Events"].Status.ShouldBe(SiteThemeFileStatus.Modified);
        files["_new"].Status.ShouldBe(SiteThemeFileStatus.Added);
        files["assets/site.css"].Status.ShouldBe(SiteThemeFileStatus.Deleted);
        files["assets/site.css"].IsAsset.ShouldBeTrue();
        files["assets/site.css"].LastModificationTime.ShouldBeNull();
        files["layouts/default"].Status.ShouldBe(SiteThemeFileStatus.Unchanged);
        files["Events"].LastModificationTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Save_A_File_And_Report_Its_Syntax_Errors()
    {
        var saved = await SaveAsync("Events", "<p>\n{{ if x }}");

        saved.Path.ShouldBe("Events");
        saved.IsAsset.ShouldBeFalse();
        saved.SyntaxErrors.ShouldNotBeEmpty();
        saved.SyntaxErrors[0].Line.ShouldBe(2);

        var read = await _themeAppService.GetFileAsync("/Events");
        read.Body.ShouldBe("<p>\n{{ if x }}");
        read.ConcurrencyStamp.ShouldBe(saved.ConcurrencyStamp);
    }

    [Fact]
    public async Task A_Static_File_Should_Have_No_Syntax_Errors()
    {
        var saved = await SaveAsync("assets/a.css", "a{{color:red}}");

        saved.IsAsset.ShouldBeTrue();
        saved.SyntaxErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Throw_Not_Found_For_A_Missing_File()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => _themeAppService.GetFileAsync("nothing"));
    }

    [Fact]
    public async Task Should_Check_With_Messages_In_The_Users_Language()
    {
        await SaveAsync("Events", "{{ include \"_missing\" }}");

        using var english = CultureHelper.Use("en");
        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeTrue();
        check.HasChanges.ShouldBeTrue();
        check.Fingerprint.Length.ShouldBe(64);
        var issue = check.Issues.First();
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Error);
        issue.Code.ShouldBe(SiteThemeIssueCodes.ReferenceMissing);
        issue.Path.ShouldBe("Events");
        issue.Message.ShouldBe("include names '_missing', which is not a file of the theme.");
        check.Changes.Select(c => c.Path).ShouldBe(new[] { "Events", "assets/site.css", "layouts/default" });
    }

    [Fact]
    public async Task Should_Put_Roles_Into_Words()
    {
        await SaveAsync("Events", "{{ include \"layouts/default\" }}");
        await SaveAsync("Default", "<p>x</p>");

        using var english = CultureHelper.Use("en");
        var check = await _themeAppService.CheckAsync();

        check.Issues.Single(i => i.Code == SiteThemeIssueCodes.MixedRoles).Message
            .ShouldBe("This file is used as a layout, and also included by another template.");
    }

    [Fact]
    public async Task Should_Publish_And_List_Releases()
    {
        await SaveAsync("Events", "<p>one</p>");
        var first = await PublishAsync("first");
        await SaveAsync("Events", "<p>two</p>");
        var second = await PublishAsync("second");

        first.Number.ShouldBe(1);
        first.IsLive.ShouldBeTrue();
        second.Number.ShouldBe(2);

        var releases = (await _themeAppService.GetReleasesAsync()).Items;
        releases.Select(r => (r.Number, r.IsLive, r.Comment)).ShouldBe(new[] { (2, true, "second"), (1, false, "first") });
        (await _themeAppService.GetAsync()).LiveReleaseNumber.ShouldBe(2);

        var detail = await _themeAppService.GetReleaseAsync(first.Id);
        detail.Files.Select(f => f.Path).ShouldBe(new[] { "Events", "assets/site.css", "layouts/default" });
        (await _themeAppService.GetReleaseFileAsync(first.Id, "Events")).Body.ShouldBe("<p>one</p>");
        await Should.ThrowAsync<EntityNotFoundException>(() => _themeAppService.GetReleaseFileAsync(first.Id, "nothing"));
    }

    [Fact]
    public async Task Should_Refuse_A_Publish_With_A_Stale_Fingerprint()
    {
        var check = await _themeAppService.CheckAsync();
        await SaveAsync("Events", "<p>x</p>");

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _themeAppService.PublishAsync(new PublishSiteThemeDto { Fingerprint = check.Fingerprint }));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeChangedSinceCheck);
    }

    [Fact]
    public async Task Should_Roll_Back_And_Restore()
    {
        await SaveAsync("Events", "<p>one</p>");
        var first = await PublishAsync();
        await SaveAsync("Events", "<p>two</p>");
        await PublishAsync();

        var live = await _themeAppService.SetLiveReleaseAsync(first.Id);
        live.IsLive.ShouldBeTrue();
        (await _themeAppService.GetFileAsync("Events")).Body.ShouldBe("<p>two</p>");

        await _themeAppService.RestoreReleaseAsync(first.Id);
        (await _themeAppService.GetFileAsync("Events")).Body.ShouldBe("<p>one</p>");
    }

    [Fact]
    public async Task Should_Change_The_Default_Layout()
    {
        await SaveAsync("layouts/landing", Layout);
        var theme = await _themeAppService.GetAsync();

        var updated = await _themeAppService.UpdateAsync(new UpdateSiteThemeDto
        {
            DefaultLayout = "layouts/landing",
            ConcurrencyStamp = theme.ConcurrencyStamp
        });

        updated.DefaultLayout.ShouldBe("layouts/landing");
        (await _themeAppService.CheckAsync()).DefaultLayout.ShouldBe("layouts/landing");
    }

    [Fact]
    public async Task Should_Refuse_A_Static_File_As_The_Default_Layout()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _themeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = "assets/site.css" }));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeDefaultLayoutInvalid);
    }

    [Fact]
    public async Task Should_Tell_Which_Pages_Still_Name_A_Renamed_Template()
    {
        await SaveAsync("Default", "<p>x</p>");

        var result = await _themeAppService.RenameFileAsync(new RenameSiteThemeFileDto { Path = "Default", NewPath = "Home" });

        result.File.Path.ShouldBe("Home");
        result.PagesStillUsingOldPath.ShouldBe(new[] { "about", "blog", "home", "news" });
    }

    [Fact]
    public async Task Should_Tell_Who_Uses_A_File()
    {
        await SaveAsync("Default", "{{ include \"_card\" }}");
        await SaveAsync("_card", "");

        var usages = await _themeAppService.GetFileUsagesAsync("_card");
        usages.Usages.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            u => u.Kind.ShouldBe(SiteThemeUsageKindDto.Include),
            u => u.SourcePath.ShouldBe("Default"));

        var pages = await _themeAppService.GetFileUsagesAsync("Default");
        pages.Usages.Select(u => u.PageName).ShouldBe(new[] { "about", "blog", "home", "news" }, ignoreOrder: true);
        pages.Usages.ShouldAllBe(u => u.Kind == SiteThemeUsageKindDto.Page);
    }

    [Fact]
    public async Task Should_Preview_The_Working_Copy()
    {
        await SaveAsync("Default", "<p>draft</p>");

        var preview = await _themeAppService.PreviewAsync("/about");

        var request = _previewRenderer.Requests.ShouldHaveSingleItem();
        request.Path.ShouldBe("/about");
        request.Snapshot.ReleaseId.ShouldBeNull();
        request.Snapshot.DefaultLayout.ShouldBe("layouts/default");
        request.Snapshot.Files.Single(f => f.Path == "Default").Body.ShouldBe("<p>draft</p>");
        request.Snapshot.Files.Single(f => f.Path == "assets/site.css").Body.ShouldNotBeNull();
        preview.Html.ShouldBe("<html data-release=\"working-copy\"><p>draft</p></html>");

        // A preview publishes nothing.
        (await _themeAppService.GetAsync()).LiveReleaseId.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Preview_A_Release()
    {
        await SaveAsync("Default", "<p>one</p>");
        var first = await PublishAsync();
        await SaveAsync("Default", "<p>two</p>");

        var preview = await _themeAppService.PreviewAsync("/about", first.Id);

        _previewRenderer.Requests.ShouldHaveSingleItem().Snapshot.ReleaseId.ShouldBe(first.Id);
        preview.Html.ShouldBe("<html data-release=\"1\"><p>one</p></html>");
    }
}
