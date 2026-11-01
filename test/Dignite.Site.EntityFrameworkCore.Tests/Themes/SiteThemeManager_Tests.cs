using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Themes;

public class SiteThemeManager_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string Layout = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>";

    private readonly SiteThemeManager _manager;
    private readonly ISiteThemeRepository _themeRepository;
    private readonly ISiteThemeFileRepository _fileRepository;
    private readonly ISiteReleaseRepository _releaseRepository;
    private readonly ISiteThemeBlobRepository _blobRepository;
    private readonly ICurrentTenant _currentTenant;

    public SiteThemeManager_Tests()
    {
        _manager = GetRequiredService<SiteThemeManager>();
        _themeRepository = GetRequiredService<ISiteThemeRepository>();
        _fileRepository = GetRequiredService<ISiteThemeFileRepository>();
        _releaseRepository = GetRequiredService<ISiteReleaseRepository>();
        _blobRepository = GetRequiredService<ISiteThemeBlobRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task<SiteTheme> ThemeAsync() => WithUnitOfWorkAsync(() => _manager.GetOrCreateAsync());

    private Task<SiteThemeFile> SaveAsync(string path, string body, string? stamp = null) =>
        WithUnitOfWorkAsync(async () => await _manager.SaveFileAsync(await _manager.GetOrCreateAsync(), path, body, stamp));

    private Task<SiteThemeCheck> CheckAsync() =>
        WithUnitOfWorkAsync(async () => await _manager.CheckAsync(await _manager.GetOrCreateAsync()));

    private async Task<SiteRelease> PublishAsync(string? comment = null)
    {
        var check = await CheckAsync();
        return await WithUnitOfWorkAsync(async () =>
            await _manager.PublishAsync(await _manager.GetOrCreateAsync(), comment, check.Fingerprint));
    }

    [Fact]
    public async Task Should_Create_The_Theme_On_First_Use_With_The_Skeleton()
    {
        var theme = await ThemeAsync();

        theme.DefaultLayout.ShouldBe(SiteThemeConsts.InitialDefaultLayout);
        theme.LiveReleaseId.ShouldBeNull();

        var files = await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync(theme.Id));
        files.Select(f => f.Path).ShouldBe(new[] { "assets/site.css", "layouts/default" });

        (await ThemeAsync()).Id.ShouldBe(theme.Id);
    }

    [Fact]
    public async Task A_New_Theme_Should_Check_Clean_But_For_The_Razor_Pages()
    {
        var check = await CheckAsync();

        check.Analysis.HasErrors.ShouldBeFalse();
        check.Analysis.Issues.Select(i => i.Code).ShouldBe(new[] { SiteThemeIssueCodes.PageRenderedByRazor });
        check.HasChanges.ShouldBeTrue();
        check.LiveRelease.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Create_And_Overwrite_A_File_In_Place()
    {
        var created = await SaveAsync("/Events", "<p>one</p>");
        var updated = await SaveAsync("Events", "<p>two</p>");

        updated.Id.ShouldBe(created.Id);
        updated.Path.ShouldBe("Events");
        var reloaded = await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(created.Id));
        reloaded.Body.ShouldBe("<p>two</p>");
        reloaded.ContentHash.ShouldBe(SiteThemePaths.ComputeContentHash("<p>two</p>"));
    }

    /// <summary>The working copy is the author's own: a template that does not parse is saved, and the check reports it.</summary>
    [Fact]
    public async Task Should_Save_A_Template_That_Does_Not_Parse()
    {
        await SaveAsync("Events", "{{ if x }}no end");

        var check = await CheckAsync();
        check.Analysis.Issues.ShouldContain(i => i.Code == SiteThemeIssueCodes.SyntaxError && i.Path == "Events");
    }

    [Fact]
    public async Task Should_Refuse_An_Extension_No_Static_File_Has()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => SaveAsync("post.html", "<p>x</p>"));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeFileExtensionNotAllowed);
    }

    [Fact]
    public async Task Should_Refuse_A_Save_With_A_Stale_Concurrency_Stamp()
    {
        var file = await SaveAsync("Events", "<p>one</p>");
        await SaveAsync("Events", "<p>someone else</p>", file.ConcurrencyStamp);

        await Should.ThrowAsync<AbpDbConcurrencyException>(() => SaveAsync("Events", "<p>mine</p>", file.ConcurrencyStamp));
    }

    [Fact]
    public async Task Should_Publish_The_Whole_Working_Copy_As_A_Release()
    {
        await SaveAsync("Events", "<h1>{{ page.name }}</h1>");

        var release = await PublishAsync("first");

        release.Number.ShouldBe(1);
        release.Comment.ShouldBe("first");
        release.DefaultLayout.ShouldBe("layouts/default");
        release.ChangedFileCount.ShouldBe(3);

        var theme = await ThemeAsync();
        theme.LiveReleaseId.ShouldBe(release.Id);

        var contents = await WithUnitOfWorkAsync(() => _releaseRepository.GetContentsAsync(release.Id, includeAssetBodies: true));
        contents.Select(c => c.Path).ShouldBe(new[] { "Events", "assets/site.css", "layouts/default" });
        contents.Single(c => c.Path == "Events").Body.ShouldBe("<h1>{{ page.name }}</h1>");

        // Without asset bodies, a static file comes with its hash only.
        var forRender = await WithUnitOfWorkAsync(() => _releaseRepository.GetContentsAsync(release.Id));
        forRender.Single(c => c.Path == "assets/site.css").Body.ShouldBeNull();
        forRender.Single(c => c.Path == "Events").Body.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Saved_File_Should_Not_Change_The_Live_Release()
    {
        await SaveAsync("Events", "<p>live</p>");
        var release = await PublishAsync();

        await SaveAsync("Events", "<p>draft</p>");

        var live = await WithUnitOfWorkAsync(() => _releaseRepository.FindContentAsync(release.Id, "Events"));
        live!.Body.ShouldBe("<p>live</p>");
    }

    [Fact]
    public async Task Should_Count_Only_What_Changed_And_Store_Each_Body_Once()
    {
        await SaveAsync("Events", "<p>one</p>");
        await SaveAsync("_copy", "<p>one</p>");
        await PublishAsync();

        await SaveAsync("Events", "<p>two</p>");
        var second = await PublishAsync();

        second.Number.ShouldBe(2);
        second.ChangedFileCount.ShouldBe(1);

        // Skeleton layout, skeleton stylesheet, "<p>one</p>" (shared by two files) and "<p>two</p>".
        (await WithUnitOfWorkAsync(() => _blobRepository.GetCountAsync())).ShouldBe(4);
    }

    [Fact]
    public async Task Should_Refuse_To_Publish_Nothing()
    {
        await PublishAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => PublishAsync());

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeNoChanges);
    }

    [Fact]
    public async Task A_Default_Layout_Change_Alone_Should_Be_Publishable_And_Recorded()
    {
        await SaveAsync("layouts/landing", Layout);
        var first = await PublishAsync();

        await WithUnitOfWorkAsync(async () => await _manager.SetDefaultLayoutAsync(await _manager.GetOrCreateAsync(), "layouts/landing"));
        var check = await CheckAsync();
        check.HasChanges.ShouldBeTrue();
        check.Changes.ShouldAllBe(c => c.Status == SiteThemeFileStatus.Unchanged);

        var second = await PublishAsync();

        first.DefaultLayout.ShouldBe("layouts/default");
        second.DefaultLayout.ShouldBe("layouts/landing");
        second.ChangedFileCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Refuse_To_Publish_With_Errors()
    {
        await SaveAsync("layouts/default", "<body>{{ body }}</body>");

        var ex = await Should.ThrowAsync<BusinessException>(() => PublishAsync());

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeHasErrors);
        ex.Data["Count"].ShouldBe(1);
        (await ThemeAsync()).LiveReleaseId.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Refuse_To_Publish_What_Changed_Since_The_Check()
    {
        var check = await CheckAsync();
        await SaveAsync("Events", "<p>saved after the check</p>");

        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
            await _manager.PublishAsync(await _manager.GetOrCreateAsync(), null, check.Fingerprint)));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeChangedSinceCheck);
    }

    [Fact]
    public async Task Should_Roll_Back_By_Making_An_Older_Release_Live()
    {
        await SaveAsync("Events", "<p>one</p>");
        var first = await PublishAsync();
        await SaveAsync("Events", "<p>two</p>");
        await PublishAsync();

        await WithUnitOfWorkAsync(async () => await _manager.SetLiveReleaseAsync(await _manager.GetOrCreateAsync(), first.Id));

        (await ThemeAsync()).LiveReleaseId.ShouldBe(first.Id);
        // The working copy is left alone.
        (await WithUnitOfWorkAsync(async () => await _manager.GetFileAsync(await _manager.GetOrCreateAsync(), "Events"))).Body
            .ShouldBe("<p>two</p>");
    }

    [Fact]
    public async Task Should_Not_Make_Another_Themes_Release_Live()
    {
        await ThemeAsync();

        await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(async () =>
            await _manager.SetLiveReleaseAsync(await _manager.GetOrCreateAsync(), Guid.NewGuid())));
    }

    [Fact]
    public async Task Should_Restore_A_Release_Into_The_Working_Copy()
    {
        await SaveAsync("Events", "<p>one</p>");
        var first = await PublishAsync();

        await SaveAsync("Events", "<p>two</p>");
        await SaveAsync("_new", "<p>new</p>");
        await WithUnitOfWorkAsync(async () => await _manager.SetDefaultLayoutAsync(await _manager.GetOrCreateAsync(), null));

        await WithUnitOfWorkAsync(async () => await _manager.RestoreReleaseAsync(await _manager.GetOrCreateAsync(), first.Id));

        var theme = await ThemeAsync();
        theme.DefaultLayout.ShouldBe("layouts/default");
        theme.LiveReleaseId.ShouldBe(first.Id);
        var files = await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync(theme.Id));
        files.Select(f => f.Path).ShouldBe(new[] { "Events", "assets/site.css", "layouts/default" });
        files.Single(f => f.Path == "Events").Body.ShouldBe("<p>one</p>");
        (await CheckAsync()).HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Rename_A_File_And_Rewrite_What_Names_It()
    {
        await SaveAsync("_card", "<div>card</div>");
        await SaveAsync("Events", "+++\nlayout = \"layouts/default\"\n+++\n{{ include \"_card\" }}[{{ include '_card' }}]");
        await SaveAsync("layouts/default", Layout + "{{ include \"_card\" }}");

        var rename = await WithUnitOfWorkAsync(async () =>
            await _manager.RenameFileAsync(await _manager.GetOrCreateAsync(), "_card", "shared/_card", updateReferences: true));

        rename.File.Path.ShouldBe("shared/_card");
        rename.UpdatedPaths.ShouldBe(new[] { "Events", "layouts/default" });
        rename.DefaultLayoutUpdated.ShouldBeFalse();

        var theme = await ThemeAsync();
        var events = await WithUnitOfWorkAsync(() => _manager.GetFileAsync(theme, "Events"));
        events.Body.ShouldBe("+++\nlayout = \"layouts/default\"\n+++\n{{ include \"shared/_card\" }}[{{ include 'shared/_card' }}]");
        (await CheckAsync()).Analysis.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public async Task Renaming_The_Default_Layout_Should_Move_The_Setting_And_Front_Matter()
    {
        await SaveAsync("Landing", "+++\nlayout = \"layouts/default\"\n+++\n<p>x</p>");

        var rename = await WithUnitOfWorkAsync(async () =>
            await _manager.RenameFileAsync(await _manager.GetOrCreateAsync(), "layouts/default", "layouts/main", updateReferences: true));

        rename.DefaultLayoutUpdated.ShouldBeTrue();
        rename.UpdatedPaths.ShouldBe(new[] { "Landing" });
        (await ThemeAsync()).DefaultLayout.ShouldBe("layouts/main");
    }

    [Fact]
    public async Task Renaming_Without_Updating_References_Should_Leave_Them()
    {
        await SaveAsync("_card", "");
        await SaveAsync("Events", "{{ include \"_card\" }}");

        await WithUnitOfWorkAsync(async () =>
            await _manager.RenameFileAsync(await _manager.GetOrCreateAsync(), "_card", "_tile", updateReferences: false));

        var check = await CheckAsync();
        check.Analysis.Issues.ShouldContain(i => i.Code == SiteThemeIssueCodes.ReferenceMissing && i.Data["Target"] == "_card");
    }

    [Fact]
    public async Task Should_Refuse_To_Rename_Onto_An_Existing_File()
    {
        await SaveAsync("_a", "");
        await SaveAsync("_b", "");

        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
            await _manager.RenameFileAsync(await _manager.GetOrCreateAsync(), "_a", "_b", updateReferences: true)));

        ex.Code.ShouldBe(SiteErrorCodes.SiteThemeFileAlreadyExists);
    }

    [Fact]
    public async Task Should_Delete_A_File_From_The_Working_Copy_Only()
    {
        await SaveAsync("Events", "<p>x</p>");
        var release = await PublishAsync();

        await WithUnitOfWorkAsync(async () => await _manager.DeleteFileAsync(await _manager.GetOrCreateAsync(), "Events"));

        var check = await CheckAsync();
        check.Changes.Single(c => c.Path == "Events").Status.ShouldBe(SiteThemeFileStatus.Deleted);
        (await WithUnitOfWorkAsync(() => _releaseRepository.FindContentAsync(release.Id, "Events"))).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Tenants_Theme_Should_Be_Its_Own()
    {
        var tenantId = Guid.NewGuid();
        Guid tenantRelease;

        using (_currentTenant.Change(tenantId))
        {
            await SaveAsync("Events", "<p>tenant</p>");
            tenantRelease = (await PublishAsync()).Id;
        }

        (await WithUnitOfWorkAsync(() => _themeRepository.FindLiveReleaseIdAsync())).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _releaseRepository.FindContentAsync(tenantRelease, "Events"))).ShouldBeNull();

        using (_currentTenant.Change(tenantId))
        {
            (await WithUnitOfWorkAsync(() => _themeRepository.FindLiveReleaseIdAsync())).ShouldBe(tenantRelease);
        }
    }
}
