using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Routing;
using Dignite.Site.Seo;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The public <c>&lt;head&gt;</c>-metadata contract, including the SeoTags flattening (总体设计 §5.3, §5.5,
/// §5.9 - GitHub issues #13, #16, #17).
/// </summary>
public class HeadMetadataPublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";

    private readonly IHeadMetadataPublicAppService _headMetadata;
    private readonly HeadMetadataPublicAppService _headMetadataConcrete;
    private readonly SiteRouteResolver _routeResolver;
    private readonly TestSettingValueProvider _settings;

    public HeadMetadataPublicAppService_Tests()
    {
        _headMetadata = GetRequiredService<IHeadMetadataPublicAppService>();
        _headMetadataConcrete = GetRequiredService<HeadMetadataPublicAppService>();
        _routeResolver = GetRequiredService<SiteRouteResolver>();
        _settings = GetRequiredService<TestSettingValueProvider>();

        _settings.Set(SiteSettings.PrimaryDomain, BaseUrl);
    }

    [Fact]
    public async Task ResolveAsync_Should_Return_Null_For_A_Path_That_Does_Not_Resolve()
    {
        var result = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = "/does-not-exist" }));

        result.ShouldBeNull();
    }

    /// <summary>The public surface never previews a draft - the same rule every other Public service follows.</summary>
    [Fact]
    public async Task ResolveAsync_Should_Return_Null_For_An_Unpublished_Slug()
    {
        var result = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{SiteTestData.DraftSlug}" }));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_Should_Return_A_Populated_Dto_For_A_Resolving_Path()
    {
        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = "/blog/my-trip" }));

        dto.ShouldNotBeNull();
        dto!.MetaTitle.ShouldBe("My trip");
        dto.CanonicalUrl.ShouldBe($"{BaseUrl}/blog/my-trip");
        dto.RobotsContent.ShouldBeNull();

        // No og:image was set for this content, so SeoTags should decide the plain summary card, not
        // summary_large_image - this service must not hard-code the large-image assumption itself. Lower
        // snake_case, not the .NET enum name: SeoTags renders "summary"/"website" in the actual HTML
        // attributes, and its own name-to-wire-value table is internal, so this DTO has to carry the wire
        // form directly rather than a value every renderer would need to re-map itself. Nothing here maps
        // the content to a schema.org shape, so OgType sits at SeoTags' own Website default.
        dto.OgImageUrl.ShouldBeNull();
        dto.TwitterCardType.ShouldBe("summary");
        dto.OgType.ShouldBe("website");
    }

    [Fact]
    public async Task TwitterCardType_Should_Be_SummaryLargeImage_When_An_OgImage_Is_Set()
    {
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();
        var slug = $"twitter-card-{Guid.NewGuid():N}";

        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime,
            ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "Has an image",
                [SeoFieldNames.FieldName] = new SeoFieldValue { OgImage = "https://acme.example/share.jpg" }
            }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}" }));

        dto!.OgImageUrl.ShouldBe("https://acme.example/share.jpg");
        dto.TwitterCardType.ShouldBe("summary_large_image");
    }

    /// <summary>
    /// The concrete proof that a future preview caller passing <c>includeUnpublished: true</c> actually
    /// gets the forced <c>noindex</c> <c>SiteRouteResolver</c>'s own doc comment demands (GitHub issue #16).
    /// </summary>
    [Fact]
    public async Task BuildDtoAsync_Should_Force_Noindex_When_Building_For_A_Preview()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _routeResolver.ResolveAsync("/blog/my-trip", SiteTestData.EnglishCulture));

        var dto = await WithUnitOfWorkAsync(() =>
            _headMetadataConcrete.BuildDtoAsync(match, SiteTestData.EnglishCulture, includeUnpublished: true));

        dto.RobotsContent.ShouldBe("noindex");
    }

    private async Task<Guid> CreateSeoEnabledContentTypeAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var fieldRepository = GetRequiredService<IFieldRepository>();
            var seoField = await fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            var titleField = await fieldRepository.GetAsync(SiteTestData.TitleFieldId);

            var contentType = await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"public-seo-test-{Guid.NewGuid():N}", "SEO test type",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, required: true, order: 0),
                    new ContentTypeField(seoField!.Id, order: 1)
                });

            return contentType.Id;
        });
    }
}
