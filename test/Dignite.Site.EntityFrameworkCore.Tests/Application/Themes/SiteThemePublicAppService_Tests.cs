using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Themes;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Themes;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Public.Themes;

/// <summary>What the site renders: the live release, and nothing of the working copy or of another tenant.</summary>
public class SiteThemePublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteThemePublicAppService _publicAppService;
    private readonly ISiteThemeAdminAppService _adminAppService;
    private readonly ICurrentTenant _currentTenant;

    public SiteThemePublicAppService_Tests()
    {
        _publicAppService = GetRequiredService<ISiteThemePublicAppService>();
        _adminAppService = GetRequiredService<ISiteThemeAdminAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task SaveAsync(string path, string body) =>
        _adminAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = path, Body = body });

    private async Task<SiteReleaseDto> PublishAsync() =>
        await _adminAppService.PublishAsync(new PublishSiteThemeDto { Fingerprint = (await _adminAppService.CheckAsync()).Fingerprint });

    [Fact]
    public async Task Nothing_Published_Should_Be_No_Release()
    {
        await _adminAppService.GetAsync();

        var live = await _publicAppService.GetLiveAsync();

        live.ReleaseId.ShouldBeNull();
        live.Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Return_The_Live_Release_With_Template_Bodies_Only()
    {
        await SaveAsync("Events", "<p>live</p>");
        var release = await PublishAsync();

        var live = await _publicAppService.GetLiveAsync();

        live.ReleaseId.ShouldBe(release.Id);
        live.IsUnchanged.ShouldBeFalse();
        live.Snapshot.ShouldNotBeNull();
        live.Snapshot.ReleaseNumber.ShouldBe(1);
        live.Snapshot.DefaultLayout.ShouldBe("layouts/default");
        live.Snapshot.Files.Single(f => f.Path == "Events").Body.ShouldBe("<p>live</p>");
        var css = live.Snapshot.Files.Single(f => f.Path == "css/site.css");
        css.Body.ShouldBeEmpty();
        css.ContentHash.ShouldBe(SiteThemePaths.ComputeContentHash(SiteThemeSkeleton.Stylesheet));
    }

    [Fact]
    public async Task A_Known_Release_Should_Come_Back_Without_Its_Files()
    {
        await PublishAsync();
        var first = await _publicAppService.GetLiveAsync();

        var again = await _publicAppService.GetLiveAsync(first.ReleaseId);

        again.ReleaseId.ShouldBe(first.ReleaseId);
        again.IsUnchanged.ShouldBeTrue();
        again.Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task The_Working_Copy_Should_Not_Show_Until_It_Is_Published()
    {
        await SaveAsync("Events", "<p>live</p>");
        await PublishAsync();
        await SaveAsync("Events", "<p>draft</p>");
        await SaveAsync("_draft", "<p>new</p>");

        var live = await _publicAppService.GetLiveAsync();

        live.Snapshot!.Files.Single(f => f.Path == "Events").Body.ShouldBe("<p>live</p>");
        live.Snapshot.Files.ShouldNotContain(f => f.Path == "_draft");
    }

    [Fact]
    public async Task A_Rollback_Should_Change_The_Live_Release()
    {
        await SaveAsync("Events", "<p>one</p>");
        var first = await PublishAsync();
        await SaveAsync("Events", "<p>two</p>");
        var second = await PublishAsync();

        (await _publicAppService.GetLiveAsync(second.Id)).IsUnchanged.ShouldBeTrue();

        await _adminAppService.SetLiveReleaseAsync(first.Id);

        var live = await _publicAppService.GetLiveAsync(second.Id);
        live.IsUnchanged.ShouldBeFalse();
        live.ReleaseId.ShouldBe(first.Id);
        live.Snapshot!.Files.Single(f => f.Path == "Events").Body.ShouldBe("<p>one</p>");
    }

    [Fact]
    public async Task Should_Find_A_Live_Static_File_With_Its_Body()
    {
        await SaveAsync("css/extra.css", "a{color:red}");
        await PublishAsync();

        var asset = await _publicAppService.FindLiveAssetAsync("css/extra.css");

        asset.ShouldNotBeNull();
        asset.Body.ShouldBe("a{color:red}");
        asset.ContentHash.ShouldBe(SiteThemePaths.ComputeContentHash("a{color:red}"));
        (await _publicAppService.FindLiveAssetAsync("/css/extra.css")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Not_Serve_A_Template_A_Draft_Or_An_Unknown_File()
    {
        await SaveAsync("Events", "<p>source</p>");
        await PublishAsync();
        await SaveAsync("css/draft.css", "a{}");

        (await _publicAppService.FindLiveAssetAsync("Events")).ShouldBeNull();
        (await _publicAppService.FindLiveAssetAsync("css/draft.css")).ShouldBeNull();
        (await _publicAppService.FindLiveAssetAsync("css/missing.css")).ShouldBeNull();
        (await _publicAppService.FindLiveAssetAsync("../secret.css")).ShouldBeNull();
    }

    [Fact]
    public async Task A_Tenants_Release_Should_Not_Reach_Another_Tenant()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await SaveAsync("css/tenant.css", "a{}");
            await PublishAsync();
            (await _publicAppService.FindLiveAssetAsync("css/tenant.css")).ShouldNotBeNull();
        }

        (await _publicAppService.GetLiveAsync()).ReleaseId.ShouldBeNull();
        (await _publicAppService.FindLiveAssetAsync("css/tenant.css")).ShouldBeNull();

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await _publicAppService.GetLiveAsync()).ReleaseId.ShouldBeNull();
        }
    }
}
