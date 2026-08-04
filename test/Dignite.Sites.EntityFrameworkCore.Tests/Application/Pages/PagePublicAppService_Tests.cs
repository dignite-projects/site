using System.Threading.Tasks;
using Dignite.Sites.Admin.Pages;
using Dignite.Sites.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Sites.Public.Pages;

public class PagePublicAppService_Tests : SitesEntityFrameworkCoreTestBase
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

        page.Id.ShouldBe(SitesTestData.BlogPageId);
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

        var list = await _pagePublicAppService.GetListAsync(new GetPageListInput { MaxResultCount = 1000 });
        list.Items.ShouldNotContain(p => p.Id == inactive.Id);
    }
}
