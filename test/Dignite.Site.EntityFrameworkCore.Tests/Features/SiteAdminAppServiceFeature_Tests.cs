using System.Threading.Tasks;
using Dignite.Site.Admin.Pages;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Pages;
using Dignite.Site.Public.Pages;
using Shouldly;
using Volo.Abp.Authorization;
using Xunit;

namespace Dignite.Site.Features;

/// <summary>
/// <see cref="SiteFeatures.Enable"/> gates the management surface and only that: the Admin application
/// services, and the MCP tools that call them. It is the Public services that must keep answering.
/// </summary>
public class SiteAdminAppServiceFeature_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IPageAdminAppService _pageAdminAppService;
    private readonly IPagePublicAppService _pagePublicAppService;
    private readonly PageTools _pageTools;
    private readonly TestFeatureValueProvider _testFeatures;

    public SiteAdminAppServiceFeature_Tests()
    {
        _pageAdminAppService = GetRequiredService<IPageAdminAppService>();
        _pagePublicAppService = GetRequiredService<IPagePublicAppService>();
        _pageTools = GetRequiredService<PageTools>();
        _testFeatures = GetRequiredService<TestFeatureValueProvider>();
    }

    [Fact]
    public async Task Should_Serve_The_Admin_Surface_While_Site_Is_Enabled()
    {
        var page = await _pageAdminAppService.GetAsync(SiteTestData.BlogPageId);

        page.Name.ShouldBe("blog");
    }

    [Fact]
    public async Task Should_Refuse_The_Admin_Surface_When_Site_Is_Disabled()
    {
        _testFeatures.Set(SiteFeatures.Enable, "false");

        await Should.ThrowAsync<AbpAuthorizationException>(
            () => _pageAdminAppService.GetAsync(SiteTestData.BlogPageId));
    }

    [Fact]
    public async Task Should_Refuse_The_Mcp_Tools_When_Site_Is_Disabled()
    {
        // The tools only call the Admin services through their interfaces, so gating the services gates
        // them too - this is the test that says so, and that the refusal happens before anything is deleted.
        _testFeatures.Set(SiteFeatures.Enable, "false");

        await Should.ThrowAsync<AbpAuthorizationException>(() => _pageTools.DeletePageAsync("blog"));

        _testFeatures.Clear();
        (await _pageAdminAppService.FindByNameAsync("blog")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Keep_Serving_The_Public_Surface_When_Site_Is_Disabled()
    {
        // Public requests are anonymous and so carry no edition claim; a gate there would read an
        // edition-granted feature as off and take the published site down (see SiteFeatures.Enable).
        _testFeatures.Set(SiteFeatures.Enable, "false");

        var page = await _pagePublicAppService.GetByRouteAsync("/blog");

        page.Id.ShouldBe(SiteTestData.BlogPageId);
    }
}
