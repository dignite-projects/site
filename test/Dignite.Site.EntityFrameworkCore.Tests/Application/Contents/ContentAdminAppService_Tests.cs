using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Validation;
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
    /// [RegularExpression] on the DTO - the admin API stores a slug verbatim (no SlugNormalizer pass, see
    /// Content.SetSlug's remarks), so this is the only thing standing between a stray space or "/" and
    /// the routing table for a slug entered here rather than through create_content.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Slug_With_An_Invalid_Format()
    {
        await Should.ThrowAsync<AbpValidationException>(() => _contentAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = "has a space",
            PublishTime = SiteTestData.PublishTime,
            FieldValues = new Dictionary<string, object?> { ["title"] = "Bad slug" }
        }));
    }

    /// <summary>
    /// The page/route-driven rules (总体设计 §3.3) surface through the app service the same way
    /// <c>ContentManager</c> throws them - not just at the domain layer <c>ContentManager_Tests</c> covers.
    /// "about" has no slug placeholder in its route at all.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Slug_Under_A_Page_Whose_Route_Has_No_Slug_Placeholder()
    {
        await Should.ThrowAsync<ContentSlugNotAllowedException>(() => _contentAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.AboutTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = "history",
            PublishTime = SiteTestData.PublishTime,
            Status = ContentStatus.Draft
        }));
    }

    /// <summary>"news" has a mandatory {slug} - every content beneath it needs one of its own.</summary>
    [Fact]
    public async Task Should_Reject_An_Empty_Slug_Under_A_Page_Whose_Route_Requires_One()
    {
        await Should.ThrowAsync<ContentSlugRequiredException>(() => _contentAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.NewsItemTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = "",
            PublishTime = SiteTestData.PublishTime,
            Status = ContentStatus.Draft
        }));
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

    /// <summary>
    /// Empty is a real address - the page's single content (总体设计 §2.4) - not "the caller omitted it".
    /// The "about" page's English content is exactly that: seeded with slug <c>""</c>.
    /// </summary>
    [Fact]
    public async Task Should_Find_A_Content_By_An_Empty_Slug()
    {
        var about = await _contentAppService.FindBySlugAsync(
            SiteTestData.AboutPageId, SiteTestData.EnglishCulture, "");

        about.ShouldNotBeNull();
        JsonSerializer.Serialize(about!.FieldValues["title"]).ShouldBe("\"About us\"");
    }

    /// <summary>
    /// A null slug is a caller error, not a synonym for the empty-slug address above -
    /// <c>IContentAdminAppService.FindBySlugAsync</c> must reject it rather than coerce it into "" and
    /// silently answer with the page's own content instead (总体设计 §2.4).
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Null_Slug()
    {
        var error = await Should.ThrowAsync<AbpValidationException>(() =>
            _contentAppService.FindBySlugAsync(SiteTestData.BlogPageId, SiteTestData.EnglishCulture, null!));

        error.ValidationErrors.ShouldContain(e => e.MemberNames.Contains("slug"));
    }

    /// <summary>
    /// The lookup normalizes the caller's culture before matching, because the stored value was
    /// normalized on write (<c>ContentManager.CreateAsync</c>) - a caller spelling the same culture
    /// differently must still find what is actually stored under its canonical form.
    /// </summary>
    [Fact]
    public async Task Should_Find_A_Content_By_A_Differently_Cased_Culture()
    {
        var content = await _contentAppService.FindBySlugAsync(
            SiteTestData.BlogPageId, "EN", SiteTestData.TripSlug);

        content.ShouldNotBeNull();
        content!.Slug.ShouldBe(SiteTestData.TripSlug);
    }

    /// <summary>
    /// A culture tag .NET does not recognize cannot be the culture of any stored row - every write path
    /// normalizes first. The honest answer is "not found", not a thrown exception that would surface to
    /// an MCP client as an opaque internal error over what is usually a one-token typo like "english" for
    /// "en" (总体设计 §6.2.4).
    /// </summary>
    [Fact]
    public async Task Should_Return_Null_Rather_Than_Throw_For_An_Unrecognized_Culture()
    {
        var content = await _contentAppService.FindBySlugAsync(
            SiteTestData.BlogPageId, "not-a-real-culture", SiteTestData.TripSlug);

        content.ShouldBeNull();
    }

    /// <summary>
    /// The list path must handle an unrecognized culture the same way the single-content lookup does, and
    /// the same way <c>EfCoreContentRepository.GetFilteredQueryableAsync</c> handles it for the query it
    /// builds: "nothing matches", not a thrown exception - this is the app-service-level pin for the
    /// culture-handling fix the repository layer carries (used by the MCP <c>list_contents</c> tool).
    /// </summary>
    [Fact]
    public async Task Should_Return_An_Empty_Page_Rather_Than_Throw_When_Listing_By_An_Unrecognized_Culture()
    {
        var result = await _contentAppService.GetListAsync(new GetContentListInput
        {
            PageId = SiteTestData.BlogPageId,
            CultureName = "english",
            MaxResultCount = 1000
        });

        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    private async Task<ContentDto> GetTripContentAsync()
    {
        var content = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug));

        content.ShouldNotBeNull();

        return await _contentAppService.GetAsync(content!.Id);
    }
}
