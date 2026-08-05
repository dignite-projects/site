using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The two text documents served at the domain root (总体设计 §5.6, GitHub issue #18). The robots rule
/// matrix itself is covered by <c>RobotsTxtBuilder_Tests</c>; what needs a database here is
/// <c>llms.txt</c>, which is built from the page table.
/// </summary>
public class RobotsDocumentAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";

    private readonly IRobotsDocumentAppService _robots;
    private readonly TestSettingValueProvider _settings;

    public RobotsDocumentAppService_Tests()
    {
        _robots = GetRequiredService<IRobotsDocumentAppService>();
        _settings = GetRequiredService<TestSettingValueProvider>();

        _settings.Set(SiteSettings.PrimaryDomain, BaseUrl);
    }

    [Fact]
    public async Task Robots_Should_Be_Plain_Text_Pointing_At_This_Tenants_Sitemap()
    {
        var document = await WithUnitOfWorkAsync(() => _robots.GetRobotsTxtAsync());

        document.ContentType.ShouldBe(RobotsDocumentAppService.PlainTextContentType);
        document.Content.ShouldContain($"Sitemap: {BaseUrl}/sitemap.xml");

        // Computed from settings, not from content - so there is no real modification time to report, and
        // reporting the generation time would be the same mistake lastmod avoids.
        document.LastModified.ShouldBeNull();
    }

    /// <summary>
    /// Off by default. Roughly 10% adoption and Google has said it does not support the file, so a site
    /// publishes it only by choosing to - and a site that has not chosen should 404 rather than serve an
    /// empty document.
    /// </summary>
    [Fact]
    public async Task Llms_Txt_Should_Not_Exist_Unless_The_Tenant_Opts_In()
    {
        (await WithUnitOfWorkAsync(() => _robots.GetLlmsTxtAsync())).ShouldBeNull();
    }

    [Fact]
    public async Task Llms_Txt_Should_List_The_Site_Pages_When_Enabled()
    {
        _settings.Set(SiteSettings.EnableLlmsTxt, "true");

        var document = await WithUnitOfWorkAsync(() => _robots.GetLlmsTxtAsync());

        document.ShouldNotBeNull();
        document!.ContentType.ShouldBe(RobotsDocumentAppService.MarkdownContentType);

        // Headed by the home page, then every routable page as an absolute link.
        document.Content.ShouldStartWith("# Home");
        document.Content.ShouldContain("## Pages");
        document.Content.ShouldContain($"- [Blog]({BaseUrl}/blog)");
        document.Content.ShouldContain($"- [About]({BaseUrl}/about)");
        document.Content.ShouldContain($"- [News]({BaseUrl}/news)");
    }
}
