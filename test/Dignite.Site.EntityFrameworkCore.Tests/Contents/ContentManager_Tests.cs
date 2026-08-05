using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Site.Contents;

/// <summary>
/// The write-path invariants of 总体设计 §2.4/§2.5: language normalization, slug uniqueness, the
/// page/content-type consistency rule, schema validation, and tenant isolation.
/// </summary>
public class ContentManager_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ContentManager _contentManager;
    private readonly IContentRepository _contentRepository;
    private readonly ICurrentTenant _currentTenant;

    public ContentManager_Tests()
    {
        _contentManager = GetRequiredService<ContentManager>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    /// <summary>
    /// The language tag is normalized on the way in, so the unique constraint and the translation-group
    /// key both see one spelling. Storing "EN" verbatim would let a second "en" row through.
    /// </summary>
    [Fact]
    public async Task Should_Normalize_Culture_On_Write()
    {
        var content = await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            SiteTestData.PostGalleryTypeId, "ZH-HANS", "culture-normalization",
            SiteTestData.PublishTime, ContentStatus.Draft,
            new Dictionary<string, object?> { ["title"] = "标题" }));

        content.CultureName.ShouldBe("zh-Hans");
    }

    [Fact]
    public async Task Should_Reject_Duplicate_Slug_In_Same_Page_And_Language()
    {
        await Should.ThrowAsync<ContentSlugAlreadyExistException>(() => WithUnitOfWorkAsync(() =>
            _contentManager.CreateAsync(
                SiteTestData.PostGalleryTypeId, SiteTestData.EnglishCulture, SiteTestData.TripSlug,
                SiteTestData.PublishTime, ContentStatus.Draft,
                new Dictionary<string, object?> { ["title"] = "Clashes with my-trip" })));
    }

    /// <summary>
    /// ...but the same slug in another language is the normal case - that is precisely how the two rows
    /// are recognized as one content's translations.
    /// </summary>
    [Fact]
    public async Task Should_Allow_Same_Slug_In_Another_Language()
    {
        var content = await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            SiteTestData.PostArticleTypeId, SiteTestData.ChineseCulture, SiteTestData.TripSlug,
            SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "我的旅行" }));

        content.Slug.ShouldBe(SiteTestData.TripSlug);
        content.CultureName.ShouldBe(SiteTestData.ChineseCulture);
    }

    /// <summary>
    /// The translation group is the natural key (PageId, ContentTypeId, Slug) - no group id anywhere.
    /// This is what hreflang and the language switcher aggregate on (§5.5).
    /// </summary>
    [Fact]
    public async Task Should_Group_Translations_By_Natural_Key()
    {
        var translations = await WithUnitOfWorkAsync(() => _contentRepository.GetTranslationsAsync(
            SiteTestData.AboutPageId, SiteTestData.AboutTypeId, ""));

        translations.Select(c => c.CultureName).OrderBy(c => c)
            .ShouldBe(new[] { SiteTestData.EnglishCulture, SiteTestData.ChineseCulture }.OrderBy(c => c));
    }

    /// <summary>
    /// A content type from another page would give the content a URL its page does not own - the two ids
    /// are read by different paths (routing reads PageId, rendering reads ContentTypeId), so a mismatch
    /// is rejected rather than reconciled.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Content_Type_From_A_Different_Page()
    {
        await Should.ThrowAsync<ContentPageInconsistentException>(() => WithUnitOfWorkAsync(async () =>
        {
            var content = (await _contentRepository.FindBySlugAsync(
                SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug))!;

            await _contentManager.UpdateAsync(
                content, content.Slug, content.PublishTime, content.Status,
                contentTypeId: SiteTestData.AboutTypeId);
        }));
    }

    /// <summary>
    /// "schema is the contract" (§1.2): title is required in post-article, so a content that omits it is
    /// refused rather than saved half-valid. The MCP write path in particular needs a hard failure.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Content_Missing_A_Required_Field()
    {
        await Should.ThrowAsync<AbpValidationException>(() => WithUnitOfWorkAsync(() =>
            _contentManager.CreateAsync(
                SiteTestData.PostArticleTypeId, SiteTestData.EnglishCulture, "missing-required-title",
                SiteTestData.PublishTime, ContentStatus.Draft,
                new Dictionary<string, object?> { ["body"] = "No title given" })));
    }

    /// <summary>
    /// A Select value outside the configured options is refused too - validation dispatches to the field
    /// type, so each type's own rules apply without Site knowing any of them.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Value_Its_Field_Type_Rejects()
    {
        await Should.ThrowAsync<AbpValidationException>(() => WithUnitOfWorkAsync(() =>
            _contentManager.CreateAsync(
                SiteTestData.PostArticleTypeId, SiteTestData.EnglishCulture, "invalid-selection",
                SiteTestData.PublishTime, ContentStatus.Draft,
                new Dictionary<string, object?>
                {
                    ["title"] = "Has a bogus category",
                    ["category"] = new List<string> { "not-an-option" }
                })));
    }

    /// <summary>
    /// A draft's publish time can never be in the future (总体设计 §2.4): a draft is hidden regardless of
    /// what PublishTime says, so a future date on one is not a schedule anything would act on.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Draft_With_Future_PublishTime()
    {
        var future = SiteTestData.PublishTime.AddYears(20);

        await Should.ThrowAsync<ContentDraftCannotHaveFuturePublishTimeException>(() => WithUnitOfWorkAsync(() =>
            _contentManager.CreateAsync(
                SiteTestData.PostArticleTypeId, SiteTestData.EnglishCulture, "future-draft",
                future, ContentStatus.Draft,
                new Dictionary<string, object?> { ["title"] = "Should not be schedulable as a draft" })));
    }

    /// <summary>
    /// The other half of the same rule: Published with a future PublishTime is exactly how scheduling for
    /// the future is done, and the existing "published, not yet due" query filter is what keeps it hidden
    /// until then - no background job required.
    /// </summary>
    [Fact]
    public async Task Should_Allow_Published_With_Future_PublishTime()
    {
        var future = SiteTestData.PublishTime.AddYears(20);

        var content = await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            SiteTestData.PostArticleTypeId, SiteTestData.EnglishCulture, "scheduled-post",
            future, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "Scheduled for later" }));

        content.Status.ShouldBe(ContentStatus.Published);
        content.IsPublished(SiteTestData.PublishTime).ShouldBeFalse();
        content.IsPublished(future).ShouldBeTrue();
    }

    /// <summary>
    /// Another tenant's site is a separate site. Its contents must not be visible from here, and it must
    /// be able to use a slug this tenant already uses.
    /// </summary>
    [Fact]
    public async Task Should_Isolate_Contents_Between_Tenants()
    {
        var otherTenantId = System.Guid.NewGuid();

        var visibleToOtherTenant = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(otherTenantId))
            {
                return await _contentRepository.GetListAsync(pageId: SiteTestData.BlogPageId);
            }
        });

        visibleToOtherTenant.ShouldBeEmpty();

        var hostContents = await WithUnitOfWorkAsync(() =>
            _contentRepository.GetListAsync(pageId: SiteTestData.BlogPageId));

        hostContents.ShouldNotBeEmpty();
    }
}
