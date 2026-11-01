using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Themes;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// What a preview renders: the working copy as saved, or any release - of the current tenant only.
/// </summary>
public class SiteThemePreviewSource_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteThemePreviewSource _source;
    private readonly ISiteThemeAdminAppService _adminAppService;
    private readonly ICurrentTenant _currentTenant;

    public SiteThemePreviewSource_Tests()
    {
        _source = GetRequiredService<ISiteThemePreviewSource>();
        _adminAppService = GetRequiredService<ISiteThemeAdminAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task SaveAsync(string path, string body) =>
        _adminAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = path, Body = body });

    private async Task<SiteReleaseDto> PublishAsync() =>
        await _adminAppService.PublishAsync(new PublishSiteThemeDto { Fingerprint = (await _adminAppService.CheckAsync()).Fingerprint });

    [Fact]
    public async Task A_Tenant_Without_A_Theme_Should_Have_Nothing_To_Preview()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await _source.FindSnapshotAsync(null)).ShouldBeNull();
            (await _source.FindAssetAsync(null, "assets/site.css")).ShouldBeNull();
        }
    }

    [Fact]
    public async Task The_Working_Copy_Should_Be_Read_As_Saved()
    {
        await SaveAsync("Default", "<p>one</p>");
        await PublishAsync();
        await SaveAsync("Default", "<p>draft</p>");
        await SaveAsync("css/draft.css", "a{}");

        var snapshot = await _source.FindSnapshotAsync(null);

        snapshot.ShouldNotBeNull();
        snapshot.ReleaseId.ShouldBeNull();
        snapshot.DefaultLayout.ShouldBe("layouts/default");
        snapshot.Files.Single(f => f.Path == "Default").Body.ShouldBe("<p>draft</p>");

        var asset = await _source.FindAssetAsync(null, "css/draft.css");
        asset.ShouldNotBeNull();
        asset.Body.ShouldBe("a{}");
    }

    [Fact]
    public async Task A_Release_Should_Be_Read_As_It_Was_Published()
    {
        await SaveAsync("Default", "<p>one</p>");
        await SaveAsync("css/site.css", "a{}");
        var first = await PublishAsync();
        await SaveAsync("Default", "<p>two</p>");
        await _adminAppService.DeleteFileAsync("css/site.css");
        await PublishAsync();

        var snapshot = await _source.FindSnapshotAsync(first.Id);

        snapshot.ShouldNotBeNull();
        snapshot.ReleaseId.ShouldBe(first.Id);
        snapshot.ReleaseNumber.ShouldBe(1);
        snapshot.Files.Single(f => f.Path == "Default").Body.ShouldBe("<p>one</p>");
        (await _source.FindAssetAsync(first.Id, "css/site.css"))!.Body.ShouldBe("a{}");
        (await _source.FindAssetAsync(null, "css/site.css")).ShouldBeNull();
    }

    [Fact]
    public async Task A_Template_Or_An_Invalid_Path_Should_Not_Be_An_Asset()
    {
        await SaveAsync("Default", "<p>x</p>");

        (await _source.FindAssetAsync(null, "Default")).ShouldBeNull();
        (await _source.FindAssetAsync(null, "../secret.css")).ShouldBeNull();
        (await _source.FindSnapshotAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task Another_Tenants_Release_Should_Not_Be_Found()
    {
        SiteReleaseDto release;
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await SaveAsync("css/tenant.css", "a{}");
            release = await PublishAsync();
            (await _source.FindSnapshotAsync(release.Id)).ShouldNotBeNull();
        }

        await _adminAppService.GetAsync();
        (await _source.FindSnapshotAsync(release.Id)).ShouldBeNull();
        (await _source.FindAssetAsync(release.Id, "css/tenant.css")).ShouldBeNull();
    }
}
