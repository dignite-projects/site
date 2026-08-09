using System.Threading.Tasks;
using Dignite.Site.Admin.Pages;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Public.Pages;

public class PagePublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IPagePublicAppService _pagePublicAppService;
    private readonly IPageAdminAppService _pageAdminAppService;

    public PagePublicAppService_Tests()
    {
        _pagePublicAppService = GetRequiredService<IPagePublicAppService>();
        _pageAdminAppService = GetRequiredService<IPageAdminAppService>();
    }

    [Fact]
    public async Task Should_Resolve_Seeded_Page_By_Route()
    {
        var page = await _pagePublicAppService.GetByRouteAsync("/blog");

        page.Id.ShouldBe(SiteTestData.BlogPageId);
    }

    [Fact]
    public async Task Should_Not_Expose_An_Inactive_Page()
    {
        var inactive = await _pageAdminAppService.CreateAsync(new CreatePageDto
        {
            Name = "public-inactive-test",
            DisplayName = "Hidden",
            Route = "/public-inactive-test",
            IsActive = false
        });

        await Should.ThrowAsync<EntityNotFoundException>(() => _pagePublicAppService.GetAsync(inactive.Id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _pagePublicAppService.GetByRouteAsync("/public-inactive-test"));

        var list = await _pagePublicAppService.GetListAsync(new GetPageListInput());
        list.Items.ShouldNotContain(p => p.Id == inactive.Id);
    }
}
