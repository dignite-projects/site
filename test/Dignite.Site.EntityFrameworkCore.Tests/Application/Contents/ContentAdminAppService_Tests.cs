using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Site.Admin.Contents;

public class ContentAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IContentAdminAppService _contentAppService;
    private readonly IContentRepository _contentRepository;

    public ContentAdminAppService_Tests()
    {
        _contentAppService = GetRequiredService<IContentAdminAppService>();
        _contentRepository = GetRequiredService<IContentRepository>();
    }

    [Fact]
    public async Task Should_Get_Seeded_Content_With_Its_Field_Values()
    {
        var content = await GetTripContentAsync();

        content.Slug.ShouldBe(SiteTestData.TripSlug);
        content.Status.ShouldBe(ContentStatus.Published);
        JsonSerializer.Serialize(content.FieldValues["title"]).ShouldBe("\"My trip\"");
        JsonSerializer.Serialize(content.FieldValues["views"]).ShouldBe("42");
        JsonSerializer.Serialize(content.FieldValues["featured"]).ShouldBe("true");
    }

    /// <summary>
    /// The DTO shape's whole reason to exist (总体设计 issue #4): a value read out through
    /// <c>ContentDto.FieldValues</c> must be postable straight back through <c>UpdateContentDto</c>
    /// unchanged, with no reshaping and no precision loss.
    /// </summary>
    [Fact]
    public async Task Should_Round_Trip_FieldValues_Unchanged_Through_An_Update_That_Does_Not_Touch_Them()
    {
        var before = await GetTripContentAsync();

        var updated = await _contentAppService.UpdateAsync(before.Id, new UpdateContentDto
        {
            Slug = before.Slug,
            PublishTime = before.PublishTime,
            Status = before.Status,
            FieldValues = before.FieldValues
        });

        JsonSerializer.Serialize(updated.FieldValues).ShouldBe(JsonSerializer.Serialize(before.FieldValues));

        var reloaded = await _contentAppService.GetAsync(before.Id);
        JsonSerializer.Serialize(reloaded.FieldValues).ShouldBe(JsonSerializer.Serialize(before.FieldValues));
    }

    [Fact]
    public async Task Should_Create_A_Content_With_A_Multi_Select_Value()
    {
        var created = await _contentAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = "admin-create-content-test",
            PublishTime = SiteTestData.PublishTime,
            Status = ContentStatus.Published,
            FieldValues = new Dictionary<string, object?>
            {
                ["title"] = "A new post",
                ["category"] = new List<string> { "tech" }
            }
        });

        created.FieldValues.ShouldContainKey("title");
        created.FieldValues.ShouldContainKey("category");
    }

    /// <summary>
    /// Pushed down to the typed query index table (总体设计 §2.4) - this is the end-to-end proof that
    /// <c>GetContentListInput.FlexFieldConditions</c> actually reaches
    /// <c>IFlexFieldQueryExecutor&lt;Content&gt;</c> and comes back with the right rows.
    /// </summary>
    [Fact]
    public async Task Should_Filter_By_A_Flex_Field_Query_Condition()
    {
        var condition = new FlexFieldQueryCondition(
            SiteTestData.ViewsFieldId, SiteTestData.ViewsFieldName,
            FlexFieldQueryOperator.GreaterThan, "10", FlexFieldValueType.Number);

        var result = await _contentAppService.GetListAsync(new GetContentListInput
        {
            PageId = SiteTestData.BlogPageId,
            FlexFieldConditions = new List<FlexFieldQueryCondition> { condition },
            MaxResultCount = 1000
        });

        // "My trip" has views = 42 (> 10); "Draft post" has views = 7 (not > 10) and must be excluded.
        result.Items.ShouldContain(c => c.Slug == SiteTestData.TripSlug);
        result.Items.ShouldNotContain(c => c.Slug == SiteTestData.DraftSlug);
    }

    private async Task<ContentDto> GetTripContentAsync()
    {
        var content = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug));

        content.ShouldNotBeNull();

        return await _contentAppService.GetAsync(content!.Id);
    }
}
