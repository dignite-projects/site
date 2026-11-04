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
        await _themeAppService.DeleteFileAsync("css/site.css");

        var files = (await _themeAppService.GetFilesAsync()).Items.ToDictionary(f => f.Path);

        files.Keys.ShouldBe(new[] { "Events", "_new", "css/site.css", "layouts/default" });
        files["Events"].Status.ShouldBe(SiteThemeFileStatus.Modified);
        files["_new"].Status.ShouldBe(SiteThemeFileStatus.Added);
        files["css/site.css"].Status.ShouldBe(SiteThemeFileStatus.Deleted);
        files["css/site.css"].IsAsset.ShouldBeTrue();
        files["css/site.css"].LastModificationTime.ShouldBeNull();
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
        check.Changes.Select(c => c.Path).ShouldBe(new[] { "Events", "css/site.css", "layouts/default" });
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
        detail.Files.Select(f => f.Path).ShouldBe(new[] { "Events", "css/site.css", "layouts/default" });
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
    public async Task The_Check_Should_Say_Which_Templates_Can_Be_The_Default_Layout()
    {
        await SaveAsync("layouts/landing", Layout);
        await SaveAsync("Events", "<p>a page</p>");

        var check = await _themeAppService.CheckAsync();

        check.Layouts.ShouldBe(new[] { "layouts/default", "layouts/landing" });
    }

    [Fact]
    public async Task Should_Refuse_A_Static_File_As_The_Default_Layout()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _themeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = "css/site.css" }));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeDefaultLayoutInvalid);
    }

    [Fact]
    public async Task Should_Refuse_A_Template_That_Is_Not_There_As_The_Default_Layout()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _themeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = "layouts/missing" }));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeDefaultLayoutNotFound);
        ex.Data["Layout"].ShouldBe("layouts/missing");
        (await _themeAppService.GetAsync()).DefaultLayout.ShouldBe("layouts/default");
    }

    [Theory]
    [InlineData("<p>a page template</p>")]
    [InlineData("<html><head>{{ site_head }}</head><body></body></html>")]
    [InlineData("<html><body>{{ body }}</body></html>")]
    [InlineData("+++\nlayout = \"layouts/default\"\n+++\n<html><head>{{ site_head }}</head><body>{{ body }}</body></html>")]
    public async Task Should_Refuse_A_Template_That_Is_Not_A_Layout_As_The_Default_Layout(string body)
    {
        await SaveAsync("layouts/not-one", body);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _themeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = "layouts/not-one" }));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeDefaultLayoutNotALayout);
        (await _themeAppService.GetAsync()).DefaultLayout.ShouldBe("layouts/default");
    }

    [Fact]
    public async Task Should_Take_A_Layout_That_Reaches_Site_Head_Through_An_Include()
    {
        await SaveAsync("_head", "{{ site_head }}");
        await SaveAsync("layouts/landing", "<html><head>{{ include \"_head\" }}</head><body>{{ body }}</body></html>");

        var updated = await _themeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = "layouts/landing" });

        updated.DefaultLayout.ShouldBe("layouts/landing");
    }

    [Fact]
    public async Task Should_Leave_A_Template_That_Does_Not_Parse_To_The_Check()
    {
        await SaveAsync("layouts/broken", "{{ if }}");

        var updated = await _themeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = "layouts/broken" });

        updated.DefaultLayout.ShouldBe("layouts/broken");
    }

    [Fact]
    public async Task Should_Clear_The_Default_Layout_With_An_Empty_Path()
    {
        var updated = await _themeAppService.UpdateAsync(new UpdateSiteThemeDto { DefaultLayout = "" });

        updated.DefaultLayout.ShouldBeNull();
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

    /// <summary>
    /// The sample is what a visitor would open: each page's own address and one published content per content type -
    /// the blog has an article and a gallery - and no draft.
    /// </summary>
    [Fact]
    public async Task The_Check_Should_Render_A_Sample_Of_The_Pages_With_The_Working_Copy()
    {
        await SaveAsync("Default", "<p>draft</p>");

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeFalse();
        check.Issues.ShouldBeEmpty();
        _previewRenderer.Requests.Select(request => request.Path).ShouldBe(
            new[]
            {
                "/", "/about", "/blog",
                "/blog/" + SiteTestData.TripSlug, "/blog/" + SiteTestData.GallerySlug,
                "/news", "/news/2026-07/launch"
            },
            ignoreOrder: true);
        _previewRenderer.Requests.ShouldAllBe(request => request.Snapshot.ReleaseId == null);
        _previewRenderer.Requests.ShouldAllBe(
            request => request.Snapshot.Files.Single(file => file.Path == "Default").Body == "<p>draft</p>");
    }

    /// <summary>The pages the theme has no template for are rendered by Razor views, which a trial render cannot run.</summary>
    [Fact]
    public async Task The_Check_Should_Not_Render_The_Pages_A_Razor_View_Renders()
    {
        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeFalse();
        _previewRenderer.Requests.ShouldBeEmpty();
    }

    /// <summary>What only a render finds: one finding for a failure, however many pages meet it, at the place the message names.</summary>
    [Fact]
    public async Task The_Check_Should_Report_A_Template_That_Fails_To_Render_Once_At_Its_Place()
    {
        await SaveAsync("Default", "{{ include \"_header\" title: page.display_name }}");
        await SaveAsync("_header", "<h1>{{ $title }}</h1>");
        _previewRenderer.Failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewRenderFailed)
            .WithData("Name", "Default")
            .WithData("Message", "_header(1,9) : error : The variable or function $title was not found")
            .WithData("File", "_header")
            .WithData("Line", 1)
            .WithData("Column", 9);

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeTrue();
        var issue = check.Issues.Single(i => i.Code == SiteThemeIssueCodes.RenderFailed);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Error);
        issue.Path.ShouldBe("_header");
        issue.Line.ShouldBe(1);
        issue.Column.ShouldBe(9);
        issue.Message.ShouldContain("$title was not found");
    }

    /// <summary>
    /// An icon whose symbol is not in the page shows nothing and fails nothing: the check says so, as a warning that does
    /// not stop a publish. A reference to an id the page does have, one to a file and `#top` are not reported.
    /// </summary>
    [Fact]
    public async Task The_Check_Should_Warn_About_A_Link_To_An_Id_The_Page_Does_Not_Have()
    {
        await SaveAsync("Default",
            "<svg><use href=\"#icon-missing\"/></svg><svg><use href=\"#icon-there\"/></svg><symbol id=\"icon-there\"/>" +
            "<svg><use href=\"sprite.svg#icon-elsewhere\"/></svg><a href=\"#top\">up</a><a href=\"#\">x</a>");

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeFalse();
        var warning = check.Issues.Single(i => i.Code == SiteThemeIssueCodes.FragmentReferenceNotFound);
        warning.Severity.ShouldBe(SiteThemeIssueSeverity.Warning);
        warning.Message.ShouldContain("icon-missing");

        // Warnings do not stop a publish.
        await _themeAppService.PublishAsync(new PublishSiteThemeDto { Fingerprint = check.Fingerprint });
    }

    /// <summary>A failure the renderer cannot place in a template is about the page's template as a whole.</summary>
    [Fact]
    public async Task The_Check_Should_Report_A_Render_Failure_Without_A_Place_Against_No_File()
    {
        await SaveAsync("Default", "<p>x</p>");
        _previewRenderer.Failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewRenderFailed)
            .WithData("Message", "timed out");

        var issue = (await _themeAppService.CheckAsync()).Issues.Single(i => i.Code == SiteThemeIssueCodes.RenderFailed);

        issue.Path.ShouldBeNull();
        issue.Line.ShouldBeNull();
    }

    [Fact]
    public async Task A_Failure_At_Render_Time_Should_Refuse_The_Publish()
    {
        await SaveAsync("Default", "<p>x</p>");
        var check = await _themeAppService.CheckAsync();
        check.HasErrors.ShouldBeFalse();

        _previewRenderer.Failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewRenderFailed)
            .WithData("Message", "Default(1,1) : error : boom");

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _themeAppService.PublishAsync(new PublishSiteThemeDto { Fingerprint = check.Fingerprint }));

        exception.Code.ShouldBe(SiteErrorCodes.SiteThemeHasErrors);
        (await _themeAppService.GetAsync()).LiveReleaseNumber.ShouldBeNull();
    }

    /// <summary>The errors the reading finds come first; rendering them would only repeat them.</summary>
    [Fact]
    public async Task The_Check_Should_Not_Render_A_Theme_That_Has_Errors()
    {
        await SaveAsync("Default", "<p>\n{{ if x }}");

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeTrue();
        check.Issues.ShouldNotContain(i => i.Code == SiteThemeIssueCodes.RenderFailed);
        _previewRenderer.Requests.ShouldBeEmpty();
    }

    /// <summary>A path that leads nowhere, or a service that fails, is not the theme's failure.</summary>
    [Theory]
    [InlineData(SiteErrorCodes.SiteTemplatePreviewPathNotFound)]
    [InlineData(SiteErrorCodes.SiteThemePreviewPageNotScriban)]
    public async Task The_Check_Should_Not_Blame_The_Theme_For_What_Is_Not_Its_Failure(string code)
    {
        await SaveAsync("Default", "<p>x</p>");
        _previewRenderer.Failure = new BusinessException(code);

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeFalse();
        check.Issues.ShouldBeEmpty();
    }

    /// <summary>It would meet the same failure at the next address, so the run ends - and says that it did not get through.</summary>
    [Fact]
    public async Task The_Check_Should_Stop_Rendering_When_A_Service_Fails_And_Say_So_Without_Failing()
    {
        await SaveAsync("Default", "<p>x</p>");
        _previewRenderer.Failure = new InvalidOperationException("the database went away");

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeFalse();
        var issue = check.Issues.ShouldHaveSingleItem();
        issue.Code.ShouldBe(SiteThemeIssueCodes.TrialRenderIncomplete);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Warning);
        _previewRenderer.Requests.ShouldHaveSingleItem();
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
        request.Snapshot.Files.Single(f => f.Path == "css/site.css").Body.ShouldNotBeNull();
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

        // The publish rendered a sample of the pages to check the theme; only the preview is counted below.
        _previewRenderer.Requests.Clear();

        var preview = await _themeAppService.PreviewAsync("/about", first.Id);

        _previewRenderer.Requests.ShouldHaveSingleItem().Snapshot.ReleaseId.ShouldBe(first.Id);
        preview.Html.ShouldBe("<html data-release=\"1\"><p>one</p></html>");
    }

    [Fact]
    public async Task Should_Issue_A_Preview_Link_For_The_Working_Copy()
    {
        var issuer = GetRequiredService<FakeSiteThemePreviewLinkIssuer>();

        var link = await _themeAppService.GetPreviewLinkAsync("/about");

        var request = issuer.Requests.ShouldHaveSingleItem();
        request.Path.ShouldBe("/about");
        request.ReleaseId.ShouldBeNull();
        link.Url.ShouldBe("https://site.example/about?site-preview=token");
    }

    [Fact]
    public async Task Should_Issue_A_Preview_Link_For_A_Release()
    {
        await SaveAsync("Default", "<p>one</p>");
        var first = await PublishAsync();
        var issuer = GetRequiredService<FakeSiteThemePreviewLinkIssuer>();

        var link = await _themeAppService.GetPreviewLinkAsync(releaseId: first.Id);

        issuer.Requests.ShouldHaveSingleItem().ReleaseId.ShouldBe(first.Id);
        link.ReleaseId.ShouldBe(first.Id);
    }

    /// <summary>Refused while the author can still be told, rather than a link that quietly shows the live release.</summary>
    [Fact]
    public async Task Should_Not_Issue_A_Preview_Link_For_An_Unknown_Release()
    {
        var issuer = GetRequiredService<FakeSiteThemePreviewLinkIssuer>();

        await Should.ThrowAsync<EntityNotFoundException>(() => _themeAppService.GetPreviewLinkAsync(releaseId: Guid.NewGuid()));

        issuer.Requests.ShouldBeEmpty();
    }
}
