using System;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The link the administration side opens: the path on the site, with a token for the current tenant, the template
/// and the version, valid for ten minutes.
/// </summary>
public class SiteTemplatePreviewLinkIssuer_Tests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (SiteTemplatePreviewLinkIssuer Issuer, SiteTemplatePreviewTestContext Preview) Create(
        SiteLanguageTestHost host,
        bool withRequest = true)
    {
        var preview = new SiteTemplatePreviewTestContext(host);
        var issuer = new SiteTemplatePreviewLinkIssuer(
            preview.Protector,
            host.CurrentTenant,
            new HttpContextAccessor { HttpContext = withRequest ? host.HttpContext : null },
            preview.Options);

        return (issuer, preview);
    }

    private static string TokenOf(string url)
    {
        return QueryHelpers.ParseQuery(new Uri(url).Query)[SiteTemplatePreviewConsts.QueryKey].ToString();
    }

    [Fact]
    public async Task The_Link_Should_Be_The_Path_On_The_Given_Site_With_A_Token()
    {
        var host = new SiteLanguageTestHost("en");
        var (issuer, preview) = Create(host);

        SiteTemplatePreviewLinkDto link;
        using (host.CurrentTenant.Change(TenantId))
        {
            link = await issuer.IssueAsync(new SiteTemplatePreviewLinkRequest
            {
                TemplateName = "Blog/Article",
                VersionNumber = 5,
                Path = "ja/blog/hello",
                BaseUrl = "https://acme.example/site/"
            });
        }

        link.Url.ShouldStartWith("https://acme.example/site/ja/blog/hello?" + SiteTemplatePreviewConsts.QueryKey + "=");
        link.Path.ShouldBe("/ja/blog/hello");
        link.TemplateName.ShouldBe("Blog/Article");
        link.VersionNumber.ShouldBe(5);
        link.ExpiresAt.ShouldBeInRange(DateTime.UtcNow.AddMinutes(9), DateTime.UtcNow.AddMinutes(11));

        preview.Protector.TryUnprotect(TokenOf(link.Url), out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBe(TenantId);
        ticket.TemplateName.ShouldBe("Blog/Article");
        ticket.VersionNumber.ShouldBe(5);
    }

    [Fact]
    public async Task Without_A_Base_Url_The_Link_Should_Go_To_The_Requests_Origin()
    {
        var host = new SiteLanguageTestHost("en", "/api/site-admin/templates/x/preview-link", pathBase: "/site");
        host.HttpContext.Request.Scheme = "https";
        host.HttpContext.Request.Host = new HostString("acme.example:8443");
        var (issuer, _) = Create(host);

        var link = await issuer.IssueAsync(new SiteTemplatePreviewLinkRequest { TemplateName = "layout", VersionNumber = 2 });

        link.Url.ShouldStartWith("https://acme.example:8443/site/?" + SiteTemplatePreviewConsts.QueryKey + "=");
        link.Path.ShouldBe("/");
    }

    [Fact]
    public async Task A_Path_With_A_Query_Should_Get_The_Token_Appended()
    {
        var host = new SiteLanguageTestHost("en");
        var (issuer, preview) = Create(host);

        var link = await issuer.IssueAsync(new SiteTemplatePreviewLinkRequest
        {
            TemplateName = "Default",
            VersionNumber = 1,
            Path = "/blog?page=2",
            BaseUrl = "https://acme.example"
        });

        link.Url.ShouldStartWith("https://acme.example/blog?page=2&" + SiteTemplatePreviewConsts.QueryKey + "=");
        preview.Protector.TryUnprotect(TokenOf(link.Url), out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Without_A_Base_Url_Or_A_Request_It_Should_Say_Why()
    {
        var host = new SiteLanguageTestHost("en");
        var (issuer, _) = Create(host, withRequest: false);

        var exception = await Should.ThrowAsync<AbpException>(
            async () => await issuer.IssueAsync(new SiteTemplatePreviewLinkRequest { TemplateName = "Default", VersionNumber = 1 }));

        exception.Message.ShouldContain(nameof(SiteTemplatePreviewLinkRequest.BaseUrl));
    }
}
