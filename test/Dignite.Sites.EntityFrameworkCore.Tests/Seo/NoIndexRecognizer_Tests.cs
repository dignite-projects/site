using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.EntityFrameworkCore;
using Dignite.Sites.Fields;
using Shouldly;
using Xunit;

namespace Dignite.Sites.Seo;

/// <summary>
/// noindex recognition end to end: resolving the seeded SEO field by its protected name, reading the
/// composite value back out of a content that has genuinely round-tripped through the database, and
/// degrading rather than throwing when the answer cannot be determined (总体设计 §5.3, GitHub issue #14).
/// </summary>
public class NoIndexRecognizer_Tests : SitesEntityFrameworkCoreTestBase
{
    private readonly NoIndexRecognizer _recognizer;
    private readonly IFieldRepository _fieldRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ContentTypeManager _contentTypeManager;
    private readonly ContentManager _contentManager;

    public NoIndexRecognizer_Tests()
    {
        _recognizer = GetRequiredService<NoIndexRecognizer>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _contentManager = GetRequiredService<ContentManager>();
    }

    [Fact]
    public async Task FindFieldAsync_Should_Resolve_The_Seeded_Field()
    {
        var field = await WithUnitOfWorkAsync(() => _recognizer.FindFieldAsync());

        field.ShouldNotBeNull();
        field!.Name.ShouldBe(SeoFieldNames.FieldName);
    }

    [Fact]
    public async Task IsNoIndex_Should_Be_False_When_The_Field_Is_Missing()
    {
        var content = await WithUnitOfWorkAsync(() => FindHomeAsync());

        _recognizer.IsNoIndex(content, null).ShouldBeFalse();
    }

    /// <summary>
    /// The seeded "home" content type never references the seo field, so nothing ever lands in the bag for
    /// it - not pulling the field in is what "pure defaults" means (总体设计 §5.3).
    /// </summary>
    [Fact]
    public async Task IsNoIndexAsync_Should_Be_False_For_A_Content_Type_That_Never_Pulled_In_Seo()
    {
        var content = await WithUnitOfWorkAsync(() => FindHomeAsync());

        (await WithUnitOfWorkAsync(() => _recognizer.IsNoIndexAsync(content))).ShouldBeFalse();
    }

    [Fact]
    public async Task IsNoIndexAsync_Should_Be_False_When_Pulled_In_But_Left_Unset()
    {
        // Committed in its own unit of work first - ContentManager.CreateAsync looks the content type up
        // through its repository, which does not see an insert that has not been saved yet.
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();

        var contentId = await WithUnitOfWorkAsync(async () =>
        {
            var content = await _contentManager.CreateAsync(
                contentTypeId, SitesTestData.EnglishCulture, "seo-unset", SitesTestData.PublishTime,
                ContentStatus.Published, new Dictionary<string, object?> { ["title"] = "No SEO set" });
            return content.Id;
        });

        // Reloaded in a fresh unit of work, so the read exercises an actual database round trip rather
        // than the same in-memory object the create call already had.
        var reloaded = await WithUnitOfWorkAsync(() => _contentRepository.GetAsync(contentId));

        (await WithUnitOfWorkAsync(() => _recognizer.IsNoIndexAsync(reloaded))).ShouldBeFalse();
    }

    [Fact]
    public async Task IsNoIndexAsync_Should_Be_True_When_Explicitly_Set()
    {
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();

        var contentId = await WithUnitOfWorkAsync(async () =>
        {
            var content = await _contentManager.CreateAsync(
                contentTypeId, SitesTestData.EnglishCulture, "seo-noindex", SitesTestData.PublishTime,
                ContentStatus.Published,
                new Dictionary<string, object?>
                {
                    ["title"] = "Noindexed",
                    [SeoFieldNames.FieldName] = new SeoFieldValue { MetaTitle = "Custom title", NoIndex = true }
                });
            return content.Id;
        });

        var reloaded = await WithUnitOfWorkAsync(() => _contentRepository.GetAsync(contentId));
        (await WithUnitOfWorkAsync(() => _recognizer.IsNoIndexAsync(reloaded))).ShouldBeTrue();

        // The rest of the bundle survived the same round trip, not just the recognized flag.
        var seoValue = await WithUnitOfWorkAsync(async () =>
        {
            var fresh = await _contentRepository.GetAsync(contentId);
            return fresh.GetField<SeoFieldValue>(SeoFieldNames.FieldName);
        });
        seoValue.MetaTitle.ShouldBe("Custom title");
    }

    /// <summary>
    /// A malformed stored value - however it got there - must not take a sitemap generation run down with
    /// it. See <see cref="NoIndexRecognizer.IsNoIndex"/>'s remarks on failing open.
    /// </summary>
    [Fact]
    public async Task IsNoIndex_Should_Degrade_To_False_On_An_Unreadable_Value()
    {
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();

        var contentId = await WithUnitOfWorkAsync(async () =>
        {
            var content = await _contentManager.CreateAsync(
                contentTypeId, SitesTestData.EnglishCulture, "seo-malformed", SitesTestData.PublishTime,
                ContentStatus.Published, new Dictionary<string, object?> { ["title"] = "Malformed" });

            // Bypasses ContentManager's own validation on purpose - simulating a value that should never
            // have been saved (direct API misuse, or a field type swapped out from under an existing value).
            content.SetField(SeoFieldNames.FieldName, "not-an-seo-object");
            await _contentRepository.UpdateAsync(content, autoSave: true);

            return content.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var reloaded = await _contentRepository.GetAsync(contentId);
            var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);

            _recognizer.IsNoIndex(reloaded, seoField).ShouldBeFalse();
        });
    }

    /// <summary>Returns the new content type's id, already committed in its own unit of work.</summary>
    private async Task<Guid> CreateSeoEnabledContentTypeAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            seoField.ShouldNotBeNull();

            var titleField = await _fieldRepository.GetAsync(SitesTestData.TitleFieldId);

            var contentType = await _contentTypeManager.CreateAsync(
                SitesTestData.BlogPageId, $"seo-test-{Guid.NewGuid():N}", "SEO test type",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, required: true, order: 0),
                    new ContentTypeField(seoField!.Id, order: 1)
                });

            return contentType.Id;
        });
    }

    private async Task<Content> FindHomeAsync()
    {
        return (await _contentRepository.FindBySlugAsync(
            SitesTestData.HomePageId, SitesTestData.EnglishCulture, ""))!;
    }
}
