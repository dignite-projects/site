using System;

namespace Dignite.Site;

/// <summary>
/// Well-known ids for the seeded scenario, so tests can address it without querying for it first.
/// </summary>
public static class SiteTestData
{
    public static readonly Guid TitleFieldId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid BodyFieldId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    public static readonly Guid CategoryFieldId = Guid.Parse("11111111-0000-0000-0000-000000000003");
    public static readonly Guid ViewsFieldId = Guid.Parse("11111111-0000-0000-0000-000000000004");
    public static readonly Guid FeaturedFieldId = Guid.Parse("11111111-0000-0000-0000-000000000005");

    public static readonly Guid HomePageId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    public static readonly Guid AboutPageId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    public static readonly Guid BlogPageId = Guid.Parse("22222222-0000-0000-0000-000000000003");
    public static readonly Guid NewsPageId = Guid.Parse("22222222-0000-0000-0000-000000000004");

    public static readonly Guid HomeTypeId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    public static readonly Guid AboutTypeId = Guid.Parse("33333333-0000-0000-0000-000000000002");
    public static readonly Guid PostArticleTypeId = Guid.Parse("33333333-0000-0000-0000-000000000003");
    public static readonly Guid PostGalleryTypeId = Guid.Parse("33333333-0000-0000-0000-000000000004");
    public static readonly Guid NewsItemTypeId = Guid.Parse("33333333-0000-0000-0000-000000000005");

    public static readonly Guid PublishedTemplateId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    public static readonly Guid DraftTemplateId = Guid.Parse("44444444-0000-0000-0000-000000000002");

    public static readonly Guid PublishedTemplateVersion1Id = Guid.Parse("45555555-0000-0000-0000-000000000001");
    public static readonly Guid PublishedTemplateVersion2Id = Guid.Parse("45555555-0000-0000-0000-000000000002");
    public static readonly Guid DraftTemplateVersion1Id = Guid.Parse("45555555-0000-0000-0000-000000000003");

    // A query condition carries both the field's id and its name: the id is what the relational index
    // rows key on, and the name is the bag key a document provider would address instead. Neither
    // substitutes for the other, so both are pinned here.
    public const string TitleFieldName = "title";
    public const string BodyFieldName = "body";
    public const string CategoryFieldName = "category";
    public const string ViewsFieldName = "views";
    public const string FeaturedFieldName = "featured";

    public const string EnglishCulture = "en";
    public const string ChineseCulture = "zh-Hans";

    /// <summary>The publish time every seeded content shares, so dated routes are predictable.</summary>
    public static readonly DateTime PublishTime = new(2026, 7, 15, 9, 30, 0, DateTimeKind.Utc);

    public const string TripSlug = "my-trip";
    public const string DraftSlug = "draft-post";
    public const string GallerySlug = "summer-photos";
    public const string NewsSlug = "launch";

    /// <summary>Two versions, the second one published. Deliberately not named layout or Default, which carry rules of their own.</summary>
    public const string PublishedTemplateName = "Seeded/Published";

    /// <summary>One version, never published.</summary>
    public const string DraftTemplateName = "Seeded/Draft";
}
