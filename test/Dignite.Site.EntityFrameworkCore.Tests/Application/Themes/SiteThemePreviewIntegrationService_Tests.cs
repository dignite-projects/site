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

/// <summary>
/// The theme of a preview for a site rendered by another application: answered for the tenant and the theme the service
/// token names, whatever tenant the call arrives as, and with nothing for any other token.
/// </summary>
public class SiteThemePreviewIntegrationService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteThemePreviewIntegrationService _service;
    private readonly ISiteThemeAdminAppService _adminAppService;
    private readonly SitePreviewServiceTokenProtector _serviceProtector;
    private readonly SitePreviewTokenProtector _browserProtector;
    private readonly ICurrentTenant _currentTenant;

    public SiteThemePreviewIntegrationService_Tests()
    {
        _service = GetRequiredService<ISiteThemePreviewIntegrationService>();
        _adminAppService = GetRequiredService<ISiteThemeAdminAppService>();
        _serviceProtector = GetRequiredService<SitePreviewServiceTokenProtector>();
        _browserProtector = GetRequiredService<SitePreviewTokenProtector>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task SaveAsync(string path, string body) =>
        _adminAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = path, Body = body });

    private string Token(Guid? tenantId = null, Guid? releaseId = null) =>
        _serviceProtector.Protect(new SitePreviewTicket(tenantId, releaseId));

    [Fact]
    public async Task Should_Read_The_Working_Copy_The_Token_Names()
    {
        await SaveAsync("Default", "<p>draft</p>");
        await SaveAsync("css/site.css", "body{}");

        var snapshot = await _service.FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = Token() });

        snapshot.ShouldNotBeNull();
        snapshot.ReleaseId.ShouldBeNull();
        snapshot.Files.Single(file => file.Path == "Default").Body.ShouldBe("<p>draft</p>");

        var asset = await _service.FindAssetAsync(new SiteThemePreviewAssetInput { Token = Token(), Path = "css/site.css" });
        asset.ShouldNotBeNull();
        asset.Body.ShouldBe("body{}");
    }

    [Fact]
    public async Task Should_Read_The_Release_The_Token_Names()
    {
        await SaveAsync("Default", "<p>one</p>");
        var release = await _adminAppService.PublishAsync(new PublishSiteThemeDto
        {
            Fingerprint = (await _adminAppService.CheckAsync()).Fingerprint
        });
        await SaveAsync("Default", "<p>two</p>");

        var snapshot = await _service.FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = Token(releaseId: release.Id) });

        snapshot!.ReleaseId.ShouldBe(release.Id);
        snapshot.Files.Single(file => file.Path == "Default").Body.ShouldBe("<p>one</p>");
    }

    /// <summary>
    /// The call comes from the site's server, which may resolve no tenant at all: the token's tenant is the one read.
    /// </summary>
    [Fact]
    public async Task Should_Read_For_The_Tenant_Of_The_Token_Not_Of_The_Call()
    {
        await SaveAsync("Default", "<p>host</p>");

        SiteThemeSnapshotDto? ofHost;
        SiteThemeSnapshotDto? ofOther;
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            ofHost = await _service.FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = Token() });
        }

        ofOther = await _service.FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = Token(Guid.NewGuid()) });

        ofHost!.Files.Single(file => file.Path == "Default").Body.ShouldBe("<p>host</p>");
        ofOther.ShouldBeNull();
    }

    /// <summary>A browser preview's token - a link that may have leaked - reads nothing here, nor does garbage.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_Read_Nothing_Without_A_Valid_Service_Token(bool browserToken)
    {
        await SaveAsync("Default", "<p>draft</p>");
        var token = browserToken
            ? _browserProtector.Protect(new SitePreviewTicket(null, null), TimeSpan.FromMinutes(1), out _)
            : "garbage";

        (await _service.FindSnapshotAsync(new SiteThemePreviewSnapshotInput { Token = token })).ShouldBeNull();
        (await _service.FindAssetAsync(new SiteThemePreviewAssetInput { Token = token, Path = "Default" })).ShouldBeNull();
    }
}
