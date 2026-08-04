using System.Linq;
using System.Threading.Tasks;
using Dignite.Sites.Contents;
using Dignite.Sites.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Sites.Public.Contents;

public class ContentPublicAppService_Tests : SitesEntityFrameworkCoreTestBase
{
    private readonly IContentPublicAppService _contentPublicAppService;
    private readonly IContentRepository _contentRepository;

    public ContentPublicAppService_Tests()
    {
        _contentPublicAppService = GetRequiredService<IContentPublicAppService>();
        _contentRepository = GetRequiredService<IContentRepository>();
    }

    [Fact]
    public async Task Should_Resolve_A_Published_Content_By_Slug()
    {
        var content = await _contentPublicAppService.GetBySlugAsync(
            SitesTestData.BlogPageId, SitesTestData.EnglishCulture, SitesTestData.TripSlug);

        content.Slug.ShouldBe(SitesTestData.TripSlug);
        content.FieldValues.ShouldContainKey("title");
    }

    [Fact]
    public async Task Should_Not_Expose_A_Draft_Content_By_Id_Or_Slug()
    {
        var draft = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SitesTestData.BlogPageId, SitesTestData.EnglishCulture, SitesTestData.DraftSlug));
        draft.ShouldNotBeNull();

        await Should.ThrowAsync<EntityNotFoundException>(() => _contentPublicAppService.GetAsync(draft!.Id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _contentPublicAppService.GetBySlugAsync(
            SitesTestData.BlogPageId, SitesTestData.EnglishCulture, SitesTestData.DraftSlug));
    }

    [Fact]
    public async Task Should_Never_List_Draft_Content_Even_Without_Asking_For_A_Status_Filter()
    {
        var result = await _contentPublicAppService.GetListAsync(
            new GetPublicContentListInput { PageId = SitesTestData.BlogPageId, MaxResultCount = 1000 });

        result.Items.ShouldNotContain(c => c.Slug == SitesTestData.DraftSlug);
        result.Items.ShouldContain(c => c.Slug == SitesTestData.TripSlug);
    }

    [Fact]
    public async Task Should_Return_Every_Published_Language_Version_As_Translations()
    {
        var translations = await _contentPublicAppService.GetTranslationsAsync(
            SitesTestData.AboutPageId, SitesTestData.AboutTypeId, "");

        translations.Items.Select(c => c.CultureName).OrderBy(c => c)
            .ShouldBe(new[] { SitesTestData.EnglishCulture, SitesTestData.ChineseCulture }.OrderBy(c => c));
    }
}
