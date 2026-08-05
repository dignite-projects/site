using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// Reading a content's mapped <c>SchemaProperty</c> values, across both storage shapes a field value can
/// arrive in, and computing which of a schema.org type's commonly-expected properties are missing
/// (总体设计 §5.4, GitHub issue #20).
/// </summary>
public class JsonLdContentDataResolver_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly JsonLdContentDataResolver _resolver;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ContentTypeManager _contentTypeManager;
    private readonly ContentManager _contentManager;

    public JsonLdContentDataResolver_Tests()
    {
        _resolver = GetRequiredService<JsonLdContentDataResolver>();
        _contentTypeRepository = GetRequiredService<IContentTypeRepository>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _contentManager = GetRequiredService<ContentManager>();
    }

    [Fact]
    public async Task Should_Return_Null_When_SchemaType_Is_None()
    {
        var contentType = await WithUnitOfWorkAsync(() =>
            _contentTypeRepository.GetAsync(SiteTestData.PostGalleryTypeId));
        var content = (await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug)))!;

        (await WithUnitOfWorkAsync(() => _resolver.ResolveAsync(contentType, content))).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Only_Include_Fields_With_A_Mapped_SchemaProperty()
    {
        var (contentTypeId, contentId) = await CreateArticleWithMappingAsync(
            new[]
            {
                new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline"),
                new ContentTypeField(SiteTestData.BodyFieldId, order: 1) // deliberately left unmapped
            },
            new Dictionary<string, object?> { ["title"] = "Headline text", ["body"] = "Body text" });

        var data = await ResolveAsync(contentTypeId, contentId);

        data.ShouldNotBeNull();
        data!.PropertyValues.Count.ShouldBe(1);
        data.PropertyValues["headline"].ShouldBe("Headline text");
    }

    /// <summary>The shape a value has before it is ever saved - still the live CLR object.</summary>
    [Fact]
    public async Task Should_Read_A_Fresh_In_Memory_Value()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () => (await _contentTypeManager.CreateAsync(
            SiteTestData.BlogPageId, $"jsonld-fresh-{Guid.NewGuid():N}", "JSON-LD fresh",
            fields: new[] { new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline") },
            schemaType: SchemaOrgType.Article)).Id);

        var data = await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeRepository.GetAsync(contentTypeId);
            var content = await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, $"jsonld-fresh-{Guid.NewGuid():N}",
                SiteTestData.PublishTime, ContentStatus.Published,
                new Dictionary<string, object?> { ["title"] = "Fresh value" });

            // Still within the same unit of work as the create - content.GetField("title") is the live
            // string, not a post-round-trip JsonElement.
            return await _resolver.ResolveAsync(contentType, content);
        });

        data!.PropertyValues["headline"].ShouldBe("Fresh value");
    }

    /// <summary>
    /// A non-scalar value is rejected in BOTH storage shapes, not just after a round trip. The seeded
    /// <c>category</c> field is multi-value (a <c>List&lt;string&gt;</c> live, a JSON array once stored), so
    /// accepting the live shape would put a raw CLR list into the JSON-LD before a save and drop it after -
    /// the same data resolving differently depending only on whether the entity was still tracked.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Non_Scalar_Value_In_Both_Storage_Shapes()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () => (await _contentTypeManager.CreateAsync(
            SiteTestData.BlogPageId, $"jsonld-nonscalar-{Guid.NewGuid():N}", "JSON-LD non-scalar",
            fields: new[]
            {
                new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline"),
                new ContentTypeField(SiteTestData.CategoryFieldId, order: 1, schemaProperty: "image")
            },
            schemaType: SchemaOrgType.Article)).Id);

        var fieldValues = new Dictionary<string, object?>
        {
            ["title"] = "Has a list",
            ["category"] = new List<string> { "travel", "food" }
        };

        // Live, in the same unit of work as the create - "category" is still a List<string> here.
        var liveData = await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeRepository.GetAsync(contentTypeId);
            var content = await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, $"nonscalar-{Guid.NewGuid():N}",
                SiteTestData.PublishTime, ContentStatus.Published, fieldValues);

            return await _resolver.ResolveAsync(contentType, content);
        });

        liveData!.PropertyValues.ShouldNotContainKey("image");

        // And after a real database round trip, where the same value is a JsonElement array.
        var (storedTypeId, storedContentId) = (contentTypeId, await WithUnitOfWorkAsync(async () =>
            (await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, $"nonscalar-stored-{Guid.NewGuid():N}",
                SiteTestData.PublishTime, ContentStatus.Published, fieldValues)).Id));

        var storedData = await ResolveAsync(storedTypeId, storedContentId);

        storedData!.PropertyValues.ShouldNotContainKey("image");
    }

    /// <summary>The shape a value arrives in once it has genuinely round-tripped through the database.</summary>
    [Fact]
    public async Task Should_Read_A_Value_That_Round_Tripped_Through_Storage()
    {
        var (contentTypeId, contentId) = await CreateArticleWithMappingAsync(
            new[] { new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline") },
            new Dictionary<string, object?> { ["title"] = "Round tripped" });

        var data = await ResolveAsync(contentTypeId, contentId);

        data!.PropertyValues["headline"].ShouldBe("Round tripped");
    }

    /// <summary>
    /// The expected list is a property of the schema.org <i>type</i>, not of this content - it is the same
    /// whether or not the content supplies any of it, because the layer that builds the node is the one
    /// that subtracts it down to what is really missing (a value can be present here and still be dropped
    /// there). <c>datePublished</c> is deliberately absent: it always comes from <c>Content.PublishTime</c>,
    /// never from a field mapping, so it could never be genuinely missing.
    /// </summary>
    [Fact]
    public async Task Should_Report_The_Types_Expected_Properties_Regardless_Of_What_Is_Supplied()
    {
        var (sparseTypeId, sparseContentId) = await CreateArticleWithMappingAsync(
            new[] { new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline") },
            new Dictionary<string, object?> { ["title"] = "Only a headline" });

        var sparse = await ResolveAsync(sparseTypeId, sparseContentId);

        sparse!.ExpectedProperties.ShouldBe(new[] { "headline", "image" }, ignoreOrder: true);
        sparse.ExpectedProperties.ShouldNotContain("datePublished");
        sparse.PropertyValues.ShouldContainKey("headline");
        sparse.PropertyValues.ShouldNotContainKey("image");

        var (fullTypeId, fullContentId) = await CreateArticleWithMappingAsync(
            new[]
            {
                new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline"),
                new ContentTypeField(SiteTestData.BodyFieldId, order: 1, schemaProperty: "image")
            },
            new Dictionary<string, object?>
            {
                ["title"] = "Headline",
                ["body"] = "https://example.com/img.jpg"
            });

        var full = await ResolveAsync(fullTypeId, fullContentId);

        full!.ExpectedProperties.ShouldBe(new[] { "headline", "image" }, ignoreOrder: true);
        full.PropertyValues.ShouldContainKey("image");
    }

    private async Task<(Guid ContentTypeId, Guid ContentId)> CreateArticleWithMappingAsync(
        IEnumerable<ContentTypeField> fields, Dictionary<string, object?> fieldValues)
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeManager.CreateAsync(
                SiteTestData.BlogPageId, $"jsonld-test-{Guid.NewGuid():N}", "JSON-LD test",
                fields: fields, schemaType: SchemaOrgType.Article);
            return contentType.Id;
        });

        var contentId = await WithUnitOfWorkAsync(async () =>
        {
            var content = await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, $"jsonld-{Guid.NewGuid():N}",
                SiteTestData.PublishTime, ContentStatus.Published, fieldValues);
            return content.Id;
        });

        return (contentTypeId, contentId);
    }

    private async Task<JsonLdContentData?> ResolveAsync(Guid contentTypeId, Guid contentId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeRepository.GetAsync(contentTypeId);
            var content = await _contentRepository.GetAsync(contentId);
            return await _resolver.ResolveAsync(contentType, content);
        });
    }
}
