using System;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Seo;
using Dignite.Site.Settings;
using Dignite.Site.Themes;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// The link the administration side opens: the path on the tenant's own site address (<c>Site.PrimaryDomain</c>), with a
/// token for the current tenant and the theme - the working copy or a release - valid for ten minutes.
/// </summary>
public class SiteThemePreviewLinkIssuer_Tests : SiteEntityFrameworkCoreTestBase
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ReleaseId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly SiteThemePreviewLinkIssuer _issuer;
    private readonly SitePreviewTokenProtector _protector;
    private readonly TestSettingValueProvider _settings;
    private readonly ICurrentTenant _currentTenant;

    public SiteThemePreviewLinkIssuer_Tests()
    {
        _issuer = GetRequiredService<SiteThemePreviewLinkIssuer>();
        _protector = GetRequiredService<SitePreviewTokenProtector>();
        _settings = GetRequiredService<TestSettingValueProvider>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string TokenOf(string url)
    {
        var key = SiteThemePreviewConsts.QueryKey + "=";
        return Uri.UnescapeDataString(url[(url.IndexOf(key, StringComparison.Ordinal) + key.Length)..]);
    }

    [Fact]
    public async Task The_Link_Should_Be_The_Path_On_The_Tenants_Primary_Domain_With_A_Token()
    {
        _settings.Set(SiteSettings.PrimaryDomain, "https://acme.example/site/");

        SiteThemePreviewLinkDto link;
        using (_currentTenant.Change(TenantId))
        {
            link = await _issuer.IssueAsync(new SiteThemePreviewLinkRequest { ReleaseId = ReleaseId, Path = "ja/blog/hello" });
        }

        link.Url.ShouldStartWith("https://acme.example/site/ja/blog/hello?" + SiteThemePreviewConsts.QueryKey + "=");
        link.Path.ShouldBe("/ja/blog/hello");
        link.ReleaseId.ShouldBe(ReleaseId);
        link.ExpiresAt.ShouldBeInRange(DateTime.UtcNow.AddMinutes(9), DateTime.UtcNow.AddMinutes(11));

        _protector.TryUnprotect(TokenOf(link.Url), out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBe(TenantId);
        ticket.ReleaseId.ShouldBe(ReleaseId);
    }

    /// <summary>A caller that knows the site's address may name it; the setting is not read then.</summary>
    [Fact]
    public async Task A_Given_Base_Url_Should_Win()
    {
        _settings.Set(SiteSettings.PrimaryDomain, "https://acme.example");

        var link = await _issuer.IssueAsync(new SiteThemePreviewLinkRequest { BaseUrl = "https://other.example/" });

        link.Url.ShouldStartWith("https://other.example/?" + SiteThemePreviewConsts.QueryKey + "=");
        _protector.TryUnprotect(TokenOf(link.Url), out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBeNull();
        ticket.ReleaseId.ShouldBeNull();
    }

    [Fact]
    public async Task A_Path_With_A_Query_Should_Get_The_Token_Appended()
    {
        _settings.Set(SiteSettings.PrimaryDomain, "https://acme.example");

        var link = await _issuer.IssueAsync(new SiteThemePreviewLinkRequest { Path = "/blog?page=2" });

        link.Url.ShouldStartWith("https://acme.example/blog?page=2&" + SiteThemePreviewConsts.QueryKey + "=");
    }

    /// <summary><c>//evil.example</c> after the site's address would still be this site, but read as a host elsewhere.</summary>
    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("\\\\evil.example/x")]
    public async Task A_Path_Should_Stay_On_The_Site(string path)
    {
        _settings.Set(SiteSettings.PrimaryDomain, "https://acme.example");

        var link = await _issuer.IssueAsync(new SiteThemePreviewLinkRequest { Path = path });

        link.Path.ShouldBe("/evil.example/x");
        link.Url.ShouldStartWith("https://acme.example/evil.example/x?");
    }

    /// <summary>Without the tenant's address there is nowhere to send the author: the same error the sitemap gives.</summary>
    [Fact]
    public async Task Without_A_Primary_Domain_It_Should_Say_So()
    {
        await Should.ThrowAsync<PrimaryDomainNotConfiguredException>(
            () => _issuer.IssueAsync(new SiteThemePreviewLinkRequest()));
    }
}
