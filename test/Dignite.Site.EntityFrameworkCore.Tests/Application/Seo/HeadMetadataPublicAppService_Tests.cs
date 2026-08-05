using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Routing;
using Dignite.Site.Seo;
using Dignite.Site.Settings;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The public <c>&lt;head&gt;</c>-metadata contract, including the SeoTags/Schema.NET flattening
/// (总体设计 §5.3, §5.4, §5.5, §5.9 - GitHub issues #13, #16, #17, #20).
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
            new ResolveHeadMetadataInput { Path = "/does-not-exist", CultureName = SiteTestData.EnglishCulture }));

        result.ShouldBeNull();
    }

    /// <summary>The public surface never previews a draft - the same rule every other Public service follows.</summary>
    [Fact]
    public async Task ResolveAsync_Should_Return_Null_For_An_Unpublished_Slug()
    {
        var result = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput
            {
                Path = $"/blog/{SiteTestData.DraftSlug}",
                CultureName = SiteTestData.EnglishCulture
            }));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_Should_Return_A_Populated_Dto_For_A_Resolving_Path()
    {
        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = "/blog/my-trip", CultureName = SiteTestData.EnglishCulture }));

        dto.ShouldNotBeNull();
        dto!.MetaTitle.ShouldBe("My trip");
        dto.CanonicalUrl.ShouldBe($"{BaseUrl}/blog/my-trip");
        dto.RobotsContent.ShouldBeNull();

        // No og:image was set for this content, so SeoTags should decide the plain summary card, not
        // summary_large_image - this service must not hard-code the large-image assumption itself. Lower
        // snake_case, not the .NET enum name: SeoTags renders "summary"/"website" in the actual HTML
        // attributes, and its own name-to-wire-value table is internal, so this DTO has to carry the wire
        // form directly rather than a value every renderer would need to re-map itself.
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
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        dto!.OgImageUrl.ShouldBe("https://acme.example/share.jpg");
        dto.TwitterCardType.ShouldBe("summary_large_image");
    }

    /// <summary>
    /// Nothing here ever calls <c>SetArticleInfo</c>/<c>SetProducInfo</c> - without an explicit assignment
    /// <c>OpenGraph.Type</c> sits at SeoTags' own Website default even for a page whose JSON-LD (built
    /// separately, from the same <c>ContentData.SchemaType</c>) already knows it is an Article.
    /// </summary>
    [Fact]
    public async Task OgType_Should_Reflect_The_Contents_Schema_Mapping()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"og-type-article-{Guid.NewGuid():N}", "OG type article",
                fields: new[] { new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline") },
                schemaType: SchemaOrgType.Article)).Id);

        var slug = $"og-type-article-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "An article" }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        dto!.OgType.ShouldBe("article");
    }

    [Fact]
    public async Task JsonLdDocuments_Should_Each_Be_Valid_Parseable_Json_With_The_Expected_Type()
    {
        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = "/blog/my-trip", CultureName = SiteTestData.EnglishCulture }));

        dto!.JsonLdDocuments.ShouldNotBeEmpty();

        var types = dto.JsonLdDocuments.Select(json =>
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("@type").GetString();
        }).ToList();

        types.ShouldContain("Organization");
        types.ShouldContain("WebSite");
        types.ShouldContain("BreadcrumbList");
    }

    [Fact]
    public async Task JsonLdDocuments_Should_Include_An_Article_Node_When_The_Content_Type_Maps_One()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"article-node-{Guid.NewGuid():N}", "Article node",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline")
                },
                schemaType: SchemaOrgType.Article)).Id);

        var slug = $"article-node-content-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime,
            ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "An article" }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        var articleJson = dto!.JsonLdDocuments.Single(json => json.Contains("\"Article\""));
        using var document = JsonDocument.Parse(articleJson);
        document.RootElement.GetProperty("headline").GetString().ShouldBe("An article");
    }

    /// <summary>
    /// Schema.NET's own serialization does not HTML-escape a string value, so tenant-authored text landing
    /// in a JSON-LD field is a stored-XSS vector once a renderer embeds the document verbatim in a
    /// <c>&lt;script type="application/ld+json"&gt;</c> block, exactly as <see cref="HeadMetadataDto.JsonLdDocuments"/>'s
    /// own doc says a renderer should.
    /// </summary>
    [Fact]
    public async Task JsonLdDocuments_Should_Escape_Angle_Brackets_So_A_Value_Cannot_Break_Out_Of_A_Script_Tag()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"xss-article-{Guid.NewGuid():N}", "XSS article",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline"),
                    new ContentTypeField(SiteTestData.BodyFieldId, order: 1, schemaProperty: "articleBody")
                },
                schemaType: SchemaOrgType.Article)).Id);

        const string payload = "Hello </script><script>alert(document.cookie)</script>";
        var slug = $"xss-article-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "XSS test", ["body"] = payload }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        var articleJson = dto!.JsonLdDocuments.Single(json => json.Contains("\"Article\""));

        articleJson.ShouldNotContain("</script>");

        // Still the same value once parsed - escaping to < must not change what a consumer reads back.
        using var document = JsonDocument.Parse(articleJson);
        document.RootElement.GetProperty("articleBody").GetString().ShouldBe(payload);
    }

    /// <summary>
    /// A tenant mapping a Text field to <c>"price"</c> and typing non-numeric text must not 500 the whole
    /// head-metadata response - every sibling reader in this pipeline fails open the same way.
    /// </summary>
    [Fact]
    public async Task JsonLdDocuments_Should_Omit_The_Offer_Rather_Than_Throw_When_Price_Is_Not_Numeric()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"product-bad-price-{Guid.NewGuid():N}", "Product bad price",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "name"),
                    new ContentTypeField(SiteTestData.BodyFieldId, order: 1, schemaProperty: "price")
                },
                schemaType: SchemaOrgType.Product)).Id);

        var slug = $"product-bad-price-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "Widget", ["body"] = "Contact us" }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        dto.ShouldNotBeNull();
        var productJson = dto!.JsonLdDocuments.Single(json => json.Contains("\"Product\""));
        productJson.ShouldNotContain("\"offers\"");
    }

    /// <summary>
    /// The canonical schema.org URL form is what <c>Schema.NET</c> itself serializes for
    /// <c>Offer.Availability</c>, and what a tenant is most likely to copy from prior output or from
    /// schema.org's own docs - an unguarded <c>Enum.TryParse</c> drops it silently since none of
    /// <see cref="ItemAvailability"/>'s members is spelled with a URL prefix.
    /// </summary>
    [Fact]
    public async Task JsonLdDocuments_Should_Accept_The_SchemaOrg_Url_Form_Of_Availability()
    {
        // "price" maps to the Number field (a whole-number "views" count in the seed, but Number is Number)
        // and "availability" to the other free-text field, so the URL string does not have to pass through
        // a Select field's fixed-option validation (category's options are travel/food/tech, not this).
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"product-availability-{Guid.NewGuid():N}", "Product availability",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "name"),
                    new ContentTypeField(SiteTestData.ViewsFieldId, order: 1, schemaProperty: "price"),
                    new ContentTypeField(SiteTestData.BodyFieldId, order: 2, schemaProperty: "availability")
                },
                schemaType: SchemaOrgType.Product)).Id);

        var slug = $"product-availability-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "Widget",
                ["views"] = 20,
                ["body"] = "https://schema.org/InStock"
            }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        var productJson = dto!.JsonLdDocuments.Single(json => json.Contains("\"Product\""));
        productJson.ShouldContain("https://schema.org/InStock");
    }

    /// <summary>
    /// A blank mapped value must not count as present, or the warning this exists to surface never fires
    /// for exactly the content that needs it (a tenant mapped the field but saved it empty). Also proves
    /// the value is actually reachable on the public DTO, not just computed and read by nothing.
    /// </summary>
    [Fact]
    public async Task MissingSchemaProperties_Should_Flag_A_Mapped_But_Blank_Property()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"missing-props-{Guid.NewGuid():N}", "Missing props",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline")
                },
                schemaType: SchemaOrgType.Article)).Id);

        var slug = $"missing-props-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "   " }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        dto!.MissingSchemaProperties.ShouldContain("headline");
        dto.MissingSchemaProperties.ShouldContain("image");
    }

    [Fact]
    public async Task MissingSchemaProperties_Should_Be_Empty_When_Every_Expected_Property_Reaches_The_Node()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"complete-article-{Guid.NewGuid():N}", "Complete article",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline"),
                    new ContentTypeField(SiteTestData.BodyFieldId, order: 1, schemaProperty: "image")
                },
                schemaType: SchemaOrgType.Article)).Id);

        var slug = $"complete-article-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "A complete article",
                ["body"] = "https://acme.example/cover.jpg"
            }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        dto!.MissingSchemaProperties.ShouldBeEmpty();
    }

    /// <summary>
    /// Presence in the value bag is not the same as reaching the emitted node: a relative URL is non-blank,
    /// so it counts as "mapped", but <c>ApplyUri</c> requires an absolute one and drops it. Reporting that
    /// content as complete is worse than not reporting at all - it says the Article has an image when the
    /// JSON-LD has none.
    /// </summary>
    [Fact]
    public async Task MissingSchemaProperties_Should_Flag_A_Value_That_Was_Present_But_Dropped_By_The_Node_Builder()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"relative-image-{Guid.NewGuid():N}", "Relative image",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline"),
                    new ContentTypeField(SiteTestData.BodyFieldId, order: 1, schemaProperty: "image")
                },
                schemaType: SchemaOrgType.Article)).Id);

        var slug = $"relative-image-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "Has a relative image",
                ["body"] = "/uploads/cover.jpg"
            }));

        var dto = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        dto!.MissingSchemaProperties.ShouldContain("image");

        var articleJson = dto.JsonLdDocuments.Single(json => json.Contains("\"Article\""));
        articleJson.ShouldNotContain("/uploads/cover.jpg");
    }

    /// <summary>
    /// Pins the emitted date to exactly what <c>IClock</c> produces, offset included. Asserting only the
    /// calendar date would be near-powerless: the seeded publish time is 09:30, so even a full ±8h
    /// server-offset error - precisely the hard-coded-UTC bug <see cref="Dignite.Site.Seo.SeoClock"/>
    /// exists to prevent - would stay inside the same day and slip through.
    /// </summary>
    [Fact]
    public async Task JsonLdDocuments_Article_Dates_Should_Match_The_Clocks_Own_Offset()
    {
        var contentTypeId = await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"article-dates-{Guid.NewGuid():N}", "Article dates",
                fields: new[] { new ContentTypeField(SiteTestData.TitleFieldId, order: 0, schemaProperty: "headline") },
                schemaType: SchemaOrgType.Article)).Id);

        var slug = $"article-dates-{Guid.NewGuid():N}";
        await WithUnitOfWorkAsync(() => GetRequiredService<ContentManager>().CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "Dated article" }));

        var result = await WithUnitOfWorkAsync(() => _headMetadata.ResolveAsync(
            new ResolveHeadMetadataInput { Path = $"/blog/{slug}", CultureName = SiteTestData.EnglishCulture }));

        var articleJson = result!.JsonLdDocuments.Single(json => json.Contains("\"Article\""));
        using var document = JsonDocument.Parse(articleJson);

        // Computed from the value as actually stored, not from the seed literal: a DateTime loses its Kind
        // on the way through the database, so the stored value comes back Unspecified and IClock resolves
        // it against the server's own offset. That is exactly what makes this assertion able to fail - a
        // hard-coded TimeSpan.Zero would emit +00:00 while the clock emits the server's real offset.
        var storedPublishTime = await WithUnitOfWorkAsync(async () =>
        {
            var content = await GetRequiredService<IContentRepository>()
                .FindBySlugAsync(SiteTestData.BlogPageId, SiteTestData.EnglishCulture, slug);
            return content!.PublishTime;
        });

        // The same conversion the sitemap and feeds use, so all three agree about one content's dates.
        var expected = GetRequiredService<IClock>().ToOffset(storedPublishTime);

        var emitted = DateTimeOffset.Parse(document.RootElement.GetProperty("datePublished").GetString()!);

        emitted.ShouldBe(expected);
        emitted.Offset.ShouldBe(expected.Offset);
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
