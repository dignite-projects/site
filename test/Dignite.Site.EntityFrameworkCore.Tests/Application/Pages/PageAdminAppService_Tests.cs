using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Admin.Pages;

public class PageAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IPageAdminAppService _pageAppService;

    public PageAdminAppService_Tests()
    {
        _pageAppService = GetRequiredService<IPageAdminAppService>();
    }

    [Fact]
    public async Task Should_Get_Seeded_Page()
    {
        var page = await _pageAppService.GetAsync(SiteTestData.BlogPageId);

        page.Name.ShouldBe("blog");
        page.Route.ShouldBe("/blog");
        page.ContentPathPattern.ShouldBe("{slug}");
    }

    [Fact]
    public async Task Should_Create_Update_And_Delete_A_Page()
    {
        var created = await _pageAppService.CreateAsync(new CreatePageDto
        {
            Name = "admin-create-test",
            DisplayName = "Contact",
            Route = "/admin-create-test",
            IsActive = true
        });

        created.Route.ShouldBe("/admin-create-test");
        created.IsHomePage.ShouldBeFalse();

        var updated = await _pageAppService.UpdateAsync(created.Id, new UpdatePageDto
        {
            Name = "admin-create-test",
            DisplayName = "Contact us",
            Route = "/admin-create-test-renamed",
            IsActive = true
        });

        updated.Route.ShouldBe("/admin-create-test-renamed");
        updated.DisplayName.ShouldBe("Contact us");

        await _pageAppService.DeleteAsync(created.Id);

        await Should.ThrowAsync<EntityNotFoundException>(() => _pageAppService.GetAsync(created.Id));
    }

    [Fact]
    public async Task Should_Exclude_Inactive_Pages_When_Filtered_Active_Only()
    {
        var inactive = await _pageAppService.CreateAsync(new CreatePageDto
        {
            Name = "admin-inactive-test",
            DisplayName = "Archived",
            Route = "/admin-inactive-test",
            IsActive = false
        });

        var result = await _pageAppService.GetListAsync(new GetPageListInput { IsActive = true, MaxResultCount = 1000 });

        result.Items.ShouldNotContain(p => p.Id == inactive.Id);
    }
}
