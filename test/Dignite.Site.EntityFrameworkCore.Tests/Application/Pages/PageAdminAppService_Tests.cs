using System.Threading.Tasks;
using Dignite.Site.Admin.ContentTypes;
using Dignite.Site.Admin.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Seo;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Admin.Pages;

public class PageAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IPageAdminAppService _pageAppService;
    private readonly IContentTypeAdminAppService _contentTypeAppService;
    private readonly IContentAdminAppService _contentAppService;
    private readonly IFieldRepository _fieldRepository;

    public PageAdminAppService_Tests()
    {
        _pageAppService = GetRequiredService<IPageAdminAppService>();
        _contentTypeAppService = GetRequiredService<IContentTypeAdminAppService>();
        _contentAppService = GetRequiredService<IContentAdminAppService>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
    }

    [Fact]
    public async Task Should_Get_Seeded_Page()
    {
        var page = await _pageAppService.GetAsync(SiteTestData.BlogPageId);

        page.Name.ShouldBe("blog");
        page.Route.ShouldBe("/blog/{slug?}");
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

    /// <summary>
    /// <c>PageManager.DeleteAsync</c>'s cascade (总体设计 §2.5), pinned here at the app-service surface -
    /// not only through the MCP <c>delete_page</c> tool, which calls the very same manager method. The
    /// seeded "blog" page carries two content types (<c>post-article</c>, <c>post-gallery</c>) and three
    /// contents between them, which is exactly the shape that exercises the manager's nested loop; the
    /// database's own foreign keys cannot do this cascade because these entities are soft-deleted, so a
    /// delete is an UPDATE and a declared ON DELETE CASCADE never fires.
    /// </summary>
    [Fact]
    public async Task Should_Cascade_Delete_Its_Content_Types_And_Contents_When_A_Page_Is_Deleted()
    {
        var tripContent = await _contentAppService.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug);
        tripContent.ShouldNotBeNull();

        var galleryContent = await _contentAppService.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.GallerySlug);
        galleryContent.ShouldNotBeNull();

        await _pageAppService.DeleteAsync(SiteTestData.BlogPageId);

        await Should.ThrowAsync<EntityNotFoundException>(() => _pageAppService.GetAsync(SiteTestData.BlogPageId));
        await Should.ThrowAsync<EntityNotFoundException>(
            () => _contentTypeAppService.GetAsync(SiteTestData.PostArticleTypeId));
        await Should.ThrowAsync<EntityNotFoundException>(
            () => _contentTypeAppService.GetAsync(SiteTestData.PostGalleryTypeId));
        await Should.ThrowAsync<EntityNotFoundException>(() => _contentAppService.GetAsync(tripContent!.Id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _contentAppService.GetAsync(galleryContent!.Id));

        // Unrelated pages, and their own content types, must survive - the cascade is scoped to this
        // page's own subtree and must not overreach past it.
        (await _pageAppService.GetAsync(SiteTestData.AboutPageId)).ShouldNotBeNull();
        (await _contentTypeAppService.GetAsync(SiteTestData.NewsItemTypeId)).ShouldNotBeNull();
    }

    /// <summary>
    /// A page starts with one content type of its own, carrying the seeded SEO field - closing the dead
    /// end where a fresh page had no content type for the Contents screen to create into (Content always
    /// needs an existing ContentType; see <c>PageAdminAppService.CreateDefaultContentTypeAsync</c>).
    /// </summary>
    [Fact]
    public async Task Should_Create_A_Default_Content_Type_Carrying_The_Seo_Field_When_A_Page_Is_Created()
    {
        var seoField = await WithUnitOfWorkAsync(() => _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName));

        var page = await _pageAppService.CreateAsync(new CreatePageDto
        {
            Name = "admin-default-content-type-test",
            DisplayName = "Default Content Type Test",
            Route = "/admin-default-content-type-test",
            IsActive = true
        });

        var contentTypes = await _contentTypeAppService.GetListByPageAsync(page.Id);

        contentTypes.Items.Count.ShouldBe(1);
        var contentType = contentTypes.Items[0];
        contentType.Name.ShouldBe(page.Name);
        contentType.DisplayName.ShouldBe(page.DisplayName);

        contentType.Fields.Count.ShouldBe(1);
        contentType.Fields[0].FieldId.ShouldBe(seoField!.Id);
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

        var result = await _pageAppService.GetListAsync(new GetPageListInput { IsActive = true });

        result.Items.ShouldNotContain(p => p.Id == inactive.Id);
    }
}
