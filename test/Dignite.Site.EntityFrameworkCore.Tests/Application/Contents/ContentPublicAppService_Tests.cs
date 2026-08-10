using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Settings;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Public.Contents;

public class ContentPublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";

    private readonly IContentPublicAppService _contentPublicAppService;
    private readonly IContentRepository _contentRepository;

    public ContentPublicAppService_Tests()
    {
        _contentPublicAppService = GetRequiredService<IContentPublicAppService>();
        _contentRepository = GetRequiredService<IContentRepository>();

        // MapToDto now computes ContentDto.Url via SiteUrlBuilder, which needs a configured base URL.
        GetRequiredService<TestSettingValueProvider>().Set(SiteSettings.PrimaryDomain, BaseUrl);
    }

    [Fact]
    public async Task Should_Resolve_A_Published_Content_By_Slug()
    {
        var content = await _contentPublicAppService.GetBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug);

        content.Slug.ShouldBe(SiteTestData.TripSlug);
        content.FieldValues.ShouldContainKey("title");
        content.Url.ShouldBe($"/blog/{SiteTestData.TripSlug}");
    }

    [Fact]
    public async Task Should_Not_Expose_A_Draft_Content_By_Id_Or_Slug()
    {
        var draft = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.DraftSlug));
        draft.ShouldNotBeNull();

        await Should.ThrowAsync<EntityNotFoundException>(() => _contentPublicAppService.GetAsync(draft!.Id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _contentPublicAppService.GetBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.DraftSlug));
    }

    [Fact]
    public async Task Should_Resolve_A_Published_Content_By_Id()
    {
        var published = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug));
        published.ShouldNotBeNull();

        var content = await _contentPublicAppService.GetAsync(published!.Id);

        content.Slug.ShouldBe(SiteTestData.TripSlug);
        content.Url.ShouldBe($"/blog/{SiteTestData.TripSlug}");
    }

    [Fact]
    public async Task Should_Never_List_Draft_Content_Even_Without_Asking_For_A_Status_Filter()
    {
        var result = await _contentPublicAppService.GetListAsync(
            new GetPublicContentListInput { PageId = SiteTestData.BlogPageId, MaxResultCount = 1000 });

        result.Items.ShouldNotContain(c => c.Slug == SiteTestData.DraftSlug);
        result.Items.ShouldContain(c => c.Slug == SiteTestData.TripSlug);
    }

    /// <summary>
    /// GetListAsync batches its Page lookup by distinct PageId (one round trip, not one per item) and maps
    /// each item's Url back through a dictionary keyed on that same PageId - a wrong key, or a batch result
    /// misaligned with its content, would only show up once a list actually spans more than one page.
    /// </summary>
    [Fact]
    public async Task Should_Compute_Each_Items_Own_Url_When_Listing_Content_Across_Multiple_Pages()
    {
        var result = await _contentPublicAppService.GetListAsync(
            new GetPublicContentListInput { MaxResultCount = 1000 });

        result.Items.Single(c => c.PageId == SiteTestData.AboutPageId && c.CultureName == SiteTestData.EnglishCulture)
            .Url.ShouldBe("/about");
        result.Items.Single(c => c.Slug == SiteTestData.TripSlug).Url.ShouldBe($"/blog/{SiteTestData.TripSlug}");
        result.Items.Single(c => c.Slug == SiteTestData.NewsSlug).Url.ShouldBe("/news/2026-07/launch");
    }

    [Fact]
    public async Task Should_Return_Every_Published_Language_Version_As_Translations()
    {
        var translations = await _contentPublicAppService.GetTranslationsAsync(
            SiteTestData.AboutPageId, SiteTestData.AboutTypeId, "");

        translations.Items.Select(c => c.CultureName).OrderBy(c => c)
            .ShouldBe(new[] { SiteTestData.EnglishCulture, SiteTestData.ChineseCulture }.OrderBy(c => c));

        translations.Items.Single(c => c.CultureName == SiteTestData.EnglishCulture).Url.ShouldBe("/about");
        translations.Items.Single(c => c.CultureName == SiteTestData.ChineseCulture).Url.ShouldBe("/zh-Hans/about");
    }
}
