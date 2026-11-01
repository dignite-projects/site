using System;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The link the administration side opens: the path on the site, with a token for the current tenant and the theme -
/// the working copy or a release - valid for ten minutes.
/// </summary>
public class SiteThemePreviewLinkIssuer_Tests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ReleaseId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static SiteThemePreviewLinkIssuer Create(SiteLanguageTestHost host, bool withRequest = true)
    {
        return new SiteThemePreviewLinkIssuer(
            host.Preview.Protector,
            host.CurrentTenant,
            new HttpContextAccessor { HttpContext = withRequest ? host.HttpContext : null },
            host.Preview.Options);
    }

    private static string TokenOf(string url)
    {
        return QueryHelpers.ParseQuery(new Uri(url).Query)[SitePreviewConsts.QueryKey].ToString();
    }

    [Fact]
    public async Task The_Link_Should_Be_The_Path_On_The_Given_Site_With_A_Token()
    {
        var host = new SiteLanguageTestHost("en");
        var issuer = Create(host);

        SiteThemePreviewLinkDto link;
        using (host.CurrentTenant.Change(TenantId))
        {
            link = await issuer.IssueAsync(new SiteThemePreviewLinkRequest
            {
                ReleaseId = ReleaseId,
                Path = "ja/blog/hello",
                BaseUrl = "https://acme.example/site/"
            });
        }

        link.Url.ShouldStartWith("https://acme.example/site/ja/blog/hello?" + SitePreviewConsts.QueryKey + "=");
        link.Path.ShouldBe("/ja/blog/hello");
        link.ReleaseId.ShouldBe(ReleaseId);
        link.ExpiresAt.ShouldBeInRange(DateTime.UtcNow.AddMinutes(9), DateTime.UtcNow.AddMinutes(11));

        host.Preview.Protector.TryUnprotect(TokenOf(link.Url), out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBe(TenantId);
        ticket.ReleaseId.ShouldBe(ReleaseId);
    }

    [Fact]
    public async Task Without_A_Base_Url_The_Link_Should_Go_To_The_Requests_Origin()
    {
        var host = new SiteLanguageTestHost("en", "/api/site-admin/theme/preview-link", pathBase: "/site");
        host.HttpContext.Request.Scheme = "https";
        host.HttpContext.Request.Host = new HostString("acme.example:8443");

        var link = await Create(host).IssueAsync(new SiteThemePreviewLinkRequest());

        link.Url.ShouldStartWith("https://acme.example:8443/site/?" + SitePreviewConsts.QueryKey + "=");
        link.Path.ShouldBe("/");
        host.Preview.Protector.TryUnprotect(TokenOf(link.Url), out var ticket).ShouldBeTrue();
        ticket!.ReleaseId.ShouldBeNull();
        ticket.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task A_Path_With_A_Query_Should_Get_The_Token_Appended()
    {
        var host = new SiteLanguageTestHost("en");

        var link = await Create(host).IssueAsync(new SiteThemePreviewLinkRequest
        {
            Path = "/blog?page=2",
            BaseUrl = "https://acme.example"
        });

        link.Url.ShouldStartWith("https://acme.example/blog?page=2&" + SitePreviewConsts.QueryKey + "=");
    }

    /// <summary><c>//evil.example</c> after the site's address would still be this site, but read as a host elsewhere.</summary>
    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("\\\\evil.example/x")]
    public async Task A_Path_Should_Stay_On_The_Site(string path)
    {
        var host = new SiteLanguageTestHost("en");

        var link = await Create(host).IssueAsync(new SiteThemePreviewLinkRequest { Path = path, BaseUrl = "https://acme.example" });

        link.Path.ShouldBe("/evil.example/x");
        link.Url.ShouldStartWith("https://acme.example/evil.example/x?");
    }

    [Fact]
    public async Task Without_A_Base_Url_Or_A_Request_It_Should_Say_Why()
    {
        var host = new SiteLanguageTestHost("en");

        var exception = await Should.ThrowAsync<AbpException>(
            async () => await Create(host, withRequest: false).IssueAsync(new SiteThemePreviewLinkRequest()));

        exception.Message.ShouldContain(nameof(SiteThemePreviewLinkRequest.BaseUrl));
    }
}
