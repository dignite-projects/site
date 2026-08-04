using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Sites.Contents;
using Dignite.Sites.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dignite.Sites.Contents;

/// <summary>
/// The FlexFields wiring end to end: values land in the authoritative bag, the derived index is
/// maintained alongside it, and a filter on a field runs as SQL against that index (总体设计 §2.4).
/// </summary>
public class ContentFlexFields_Tests : SitesEntityFrameworkCoreTestBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IRepository<ContentFlexFieldIndex, System.Guid> _indexRepository;
    private readonly ContentManager _contentManager;

    public ContentFlexFields_Tests()
    {
        _contentRepository = GetRequiredService<IContentRepository>();
        _indexRepository = GetRequiredService<IRepository<ContentFlexFieldIndex, System.Guid>>();
        _contentManager = GetRequiredService<ContentManager>();
    }

    [Fact]
    public async Task Should_Store_And_Read_Back_Field_Values()
    {
        var content = await WithUnitOfWorkAsync(() => FindTripAsync());

        content.GetField<string>("title").ShouldBe("My trip");
        content.GetField<string>("body").ShouldBe("Trip body");
        content.GetField<decimal>("views").ShouldBe(42);
        content.GetField<bool>("featured").ShouldBeTrue();
        content.GetField<List<string>>("category").ShouldBe(new List<string> { "travel", "food" });
    }

    /// <summary>
    /// Only the searchable usages produce index rows. body is pulled into the same content type but not
    /// marked searchable, so it has a value in the bag and nothing in the index - which is the per-usage
    /// Searchable flag doing its job.
    /// </summary>
    [Fact]
    public async Task Should_Index_Only_Searchable_Usages()
    {
        var rows = await WithUnitOfWorkAsync(async () =>
        {
            var content = await FindTripAsync();
            return await _indexRepository.GetListAsync(x => x.ContentId == content.Id);
        });

        var indexedFieldIds = rows.Select(r => r.FieldId).Distinct().ToList();

        indexedFieldIds.ShouldContain(SitesTestData.TitleFieldId);
        indexedFieldIds.ShouldContain(SitesTestData.CategoryFieldId);
        indexedFieldIds.ShouldContain(SitesTestData.ViewsFieldId);
        indexedFieldIds.ShouldContain(SitesTestData.FeaturedFieldId);

        indexedFieldIds.ShouldNotContain(SitesTestData.BodyFieldId);
    }

    /// <summary>
    /// Each value lands in the column its field type names, not stringified into one. This is what lets a
    /// numeric comparison be a numeric comparison in SQL.
    /// </summary>
    [Fact]
    public async Task Should_Write_Values_Into_Their_Typed_Slots()
    {
        var rows = await WithUnitOfWorkAsync(async () =>
        {
            var content = await FindTripAsync();
            return await _indexRepository.GetListAsync(x => x.ContentId == content.Id);
        });

        var views = rows.Single(r => r.FieldId == SitesTestData.ViewsFieldId);
        views.ValueType.ShouldBe(FlexFieldValueType.Number);
        views.NumberValue.ShouldBe(42);
        views.StringValue.ShouldBeNull();

        var featured = rows.Single(r => r.FieldId == SitesTestData.FeaturedFieldId);
        featured.ValueType.ShouldBe(FlexFieldValueType.Boolean);
        featured.BooleanValue.ShouldBe(true);

        var title = rows.Single(r => r.FieldId == SitesTestData.TitleFieldId);
        title.ValueType.ShouldBe(FlexFieldValueType.String);
        title.StringValue.ShouldBe("My trip");
    }

    /// <summary>
    /// A multi-valued field decomposes into one row per value, so "has category food" can be an index
    /// lookup rather than a scan of a serialized list.
    /// </summary>
    [Fact]
    public async Task Should_Write_One_Row_Per_Value_Of_A_Multi_Valued_Field()
    {
        var rows = await WithUnitOfWorkAsync(async () =>
        {
            var content = await FindTripAsync();
            return await _indexRepository.GetListAsync(x => x.ContentId == content.Id);
        });

        rows.Where(r => r.FieldId == SitesTestData.CategoryFieldId)
            .Select(r => r.StringValue)
            .OrderBy(v => v)
            .ShouldBe(new[] { "food", "travel" });
    }

    [Fact]
    public async Task Should_Filter_By_Field_Value()
    {
        var results = await WithUnitOfWorkAsync(() => _contentRepository.GetListAsync(
            pageId: SitesTestData.BlogPageId,
            flexFieldConditions: new[]
            {
                new FlexFieldQueryCondition(
                    SitesTestData.CategoryFieldId, SitesTestData.CategoryFieldName,
                    FlexFieldQueryOperator.Equals, "travel", FlexFieldValueType.String)
            }));

        results.Count.ShouldBe(1);
        results[0].Slug.ShouldBe(SitesTestData.TripSlug);
    }

    /// <summary>
    /// A numeric comparison, which is the case that would have been impossible without typed slots.
    /// </summary>
    [Fact]
    public async Task Should_Filter_By_Numeric_Comparison()
    {
        var above = await WithUnitOfWorkAsync(() => _contentRepository.GetListAsync(
            pageId: SitesTestData.BlogPageId,
            flexFieldConditions: new[]
            {
                new FlexFieldQueryCondition(
                    SitesTestData.ViewsFieldId, SitesTestData.ViewsFieldName,
                    FlexFieldQueryOperator.GreaterThan, "10", FlexFieldValueType.Number)
            }));

        // my-trip has 42; draft-post has 7 and is excluded by the comparison, not by its status.
        above.Select(c => c.Slug).ShouldBe(new[] { SitesTestData.TripSlug });
    }

    /// <summary>
    /// Conditions are ANDed - each becomes its own subquery composed onto the same content query.
    /// </summary>
    [Fact]
    public async Task Should_And_Multiple_Conditions()
    {
        var matching = await WithUnitOfWorkAsync(() => _contentRepository.GetListAsync(
            pageId: SitesTestData.BlogPageId,
            flexFieldConditions: new[]
            {
                new FlexFieldQueryCondition(
                    SitesTestData.CategoryFieldId, SitesTestData.CategoryFieldName,
                    FlexFieldQueryOperator.Equals, "travel", FlexFieldValueType.String),
                new FlexFieldQueryCondition(
                    SitesTestData.FeaturedFieldId, SitesTestData.FeaturedFieldName,
                    FlexFieldQueryOperator.Equals, "true", FlexFieldValueType.Boolean)
            }));

        matching.Select(c => c.Slug).ShouldBe(new[] { SitesTestData.TripSlug });

        var contradictory = await WithUnitOfWorkAsync(() => _contentRepository.GetListAsync(
            pageId: SitesTestData.BlogPageId,
            flexFieldConditions: new[]
            {
                new FlexFieldQueryCondition(
                    SitesTestData.CategoryFieldId, SitesTestData.CategoryFieldName,
                    FlexFieldQueryOperator.Equals, "travel", FlexFieldValueType.String),
                new FlexFieldQueryCondition(
                    SitesTestData.CategoryFieldId, SitesTestData.CategoryFieldName,
                    FlexFieldQueryOperator.Equals, "tech", FlexFieldValueType.String)
            }));

        contradictory.ShouldBeEmpty();
    }

    /// <summary>
    /// The index is derived state, so it has to track an edit - including values that were removed, whose
    /// rows must go rather than linger and keep matching.
    /// </summary>
    [Fact]
    public async Task Should_Resynchronize_Index_On_Update()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var content = await FindTripAsync();

            await _contentManager.UpdateAsync(
                content, content.Slug, content.PublishTime, content.Status,
                new Dictionary<string, object?>
                {
                    ["title"] = "My trip, revised",
                    ["category"] = new List<string> { "tech" }
                    // views and featured deliberately dropped
                });
        });

        var rows = await WithUnitOfWorkAsync(async () =>
        {
            var content = await FindTripAsync();
            return await _indexRepository.GetListAsync(x => x.ContentId == content.Id);
        });

        rows.Single(r => r.FieldId == SitesTestData.TitleFieldId).StringValue.ShouldBe("My trip, revised");
        rows.Single(r => r.FieldId == SitesTestData.CategoryFieldId).StringValue.ShouldBe("tech");
        rows.ShouldNotContain(r => r.FieldId == SitesTestData.ViewsFieldId);
        rows.ShouldNotContain(r => r.FieldId == SitesTestData.FeaturedFieldId);
    }

    /// <summary>
    /// A bag is the storage for a declared schema, not a free-form dictionary: a key the content type
    /// does not declare is dropped rather than stored, since nothing would validate, index or read it.
    /// </summary>
    [Fact]
    public async Task Should_Drop_Values_The_Content_Type_Does_Not_Declare()
    {
        var content = await WithUnitOfWorkAsync(async () =>
        {
            var created = await _contentManager.CreateAsync(
                SitesTestData.PostArticleTypeId, SitesTestData.EnglishCulture, "undeclared-key-test",
                SitesTestData.PublishTime, ContentStatus.Draft,
                new Dictionary<string, object?>
                {
                    ["title"] = "Has an undeclared key",
                    ["not-a-declared-field"] = "should not be stored"
                });

            return created;
        });

        content.HasField("title").ShouldBeTrue();
        content.HasField("not-a-declared-field").ShouldBeFalse();
    }

    private async Task<Content> FindTripAsync()
    {
        return (await _contentRepository.FindBySlugAsync(
            SitesTestData.BlogPageId, SitesTestData.EnglishCulture, SitesTestData.TripSlug))!;
    }
}
