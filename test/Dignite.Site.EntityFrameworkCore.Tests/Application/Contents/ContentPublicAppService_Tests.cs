using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Settings;
using Dignite.Site.Timing;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Public.Contents;

public class ContentPublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";

    private readonly IContentPublicAppService _contentPublicAppService;
    private readonly IContentAdminAppService _contentAdminAppService;
    private readonly IContentRepository _contentRepository;

    public ContentPublicAppService_Tests()
    {
        _contentPublicAppService = GetRequiredService<IContentPublicAppService>();
        _contentAdminAppService = GetRequiredService<IContentAdminAppService>();
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

    /// <summary>
    /// Unlike a draft, an archived content was already public once - its detail path keeps answering by id
    /// and by slug so a link already indexed or bookmarked from before it was archived does not go dead
    /// (<see cref="Content.IsPubliclyAccessible"/>).
    /// </summary>
    [Fact]
    public async Task Should_Expose_An_Archived_Content_By_Id_Or_Slug()
    {
        var archived = await _contentAdminAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = "archived-detail-test",
            PublishTime = SiteTestData.PublishTime,
            Status = ContentStatus.Archived,
            FieldValues = new Dictionary<string, object?> { ["title"] = "Archived detail test" }
        });

        (await _contentPublicAppService.GetAsync(archived.Id)).Slug.ShouldBe("archived-detail-test");

        (await _contentPublicAppService.GetBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, "archived-detail-test"))
            .Slug.ShouldBe("archived-detail-test");
    }

    [Fact]
    public async Task Should_Never_List_Archived_Content_Even_Without_Asking_For_A_Status_Filter()
    {
        await _contentAdminAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = "archived-list-test",
            PublishTime = SiteTestData.PublishTime,
            Status = ContentStatus.Archived,
            FieldValues = new Dictionary<string, object?> { ["title"] = "Archived list test" }
        });

        var result = await _contentPublicAppService.GetListAsync(
            new GetContentListInput { PageId = SiteTestData.BlogPageId, MaxResultCount = 1000 });

        result.Items.ShouldNotContain(c => c.Slug == "archived-list-test");
        result.Items.ShouldContain(c => c.Slug == SiteTestData.TripSlug);
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
            new GetContentListInput { PageId = SiteTestData.BlogPageId, MaxResultCount = 1000 });

        result.Items.ShouldNotContain(c => c.Slug == SiteTestData.DraftSlug);
        result.Items.ShouldContain(c => c.Slug == SiteTestData.TripSlug);
    }

    [Fact]
    public async Task Should_Narrow_By_PublishedAfter()
    {
        var result = await _contentPublicAppService.GetListAsync(new GetContentListInput
        {
            PageId = SiteTestData.BlogPageId,
            PublishedAfter = SiteTestData.PublishTime.AddDays(1),
            MaxResultCount = 1000
        });

        result.Items.ShouldNotContain(c => c.Slug == SiteTestData.TripSlug);
    }

    /// <summary>
    /// A content can be Status=Published with a future PublishTime - a scheduled publish (总体设计 §2.4) -
    /// and must stay invisible until that instant regardless of what a caller passes as PublishedBefore.
    /// GetContentListInput's own doc comment promises "published as of now" unconditionally; this is the
    /// case that promise actually has to hold against, since a generous-but-honest caller (e.g. building a
    /// "this month" archive filter) could otherwise accidentally leak next week's scheduled post.
    /// </summary>
    [Fact]
    public async Task Should_Not_Let_PublishedBefore_Reveal_A_Content_Scheduled_After_Now()
    {
        var clock = GetRequiredService<TestClock>();
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        clock.Set(now);

        var future = now.AddDays(10);
        var scheduled = await _contentAdminAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = "scheduled-future-test",
            PublishTime = future,
            Status = ContentStatus.Published,
            FieldValues = new Dictionary<string, object?> { ["title"] = "Scheduled future post" }
        });

        var unfiltered = await _contentPublicAppService.GetListAsync(
            new GetContentListInput { PageId = SiteTestData.BlogPageId, MaxResultCount = 1000 });
        unfiltered.Items.ShouldNotContain(c => c.Id == scheduled.Id);

        var filtered = await _contentPublicAppService.GetListAsync(new GetContentListInput
        {
            PageId = SiteTestData.BlogPageId,
            PublishedBefore = future.AddDays(1), // generous - past the content's own future PublishTime
            MaxResultCount = 1000
        });
        filtered.Items.ShouldNotContain(c => c.Id == scheduled.Id);
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
            new GetContentListInput { MaxResultCount = 1000 });

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

    /// <summary>
    /// The language switcher must not go blind on an archived page: a sibling translation that is also
    /// archived is exactly as reachable as the one being viewed, so it belongs in the switcher too.
    /// </summary>
    [Fact]
    public async Task Should_Include_An_Archived_Translation()
    {
        const string slug = "archived-translation-test";

        await _contentAdminAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = slug,
            PublishTime = SiteTestData.PublishTime,
            Status = ContentStatus.Published,
            FieldValues = new Dictionary<string, object?> { ["title"] = "English" }
        });

        await _contentAdminAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.ChineseCulture,
            Slug = slug,
            PublishTime = SiteTestData.PublishTime,
            Status = ContentStatus.Archived,
            FieldValues = new Dictionary<string, object?> { ["title"] = "中文" }
        });

        var translations = await _contentPublicAppService.GetTranslationsAsync(
            SiteTestData.BlogPageId, SiteTestData.PostArticleTypeId, slug);

        translations.Items.Select(c => c.CultureName).OrderBy(c => c)
            .ShouldBe(new[] { SiteTestData.EnglishCulture, SiteTestData.ChineseCulture }.OrderBy(c => c));
    }

    // GetAdjacentAsync. Every content these create is in AdjacentCulture, which the seed never uses, so the
    // seeded blog posts - all sharing one PublishTime - never become anyone's neighbor by accident.

    private const string AdjacentCulture = "fr";

    private static readonly DateTime AdjacentBaseTime = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAdjacent_Should_Return_The_Older_As_Previous_And_The_Newer_As_Next()
    {
        var older = await CreateAdjacentTestContentAsync("adj-older", AdjacentBaseTime);
        var anchor = await CreateAdjacentTestContentAsync("adj-anchor", AdjacentBaseTime.AddDays(1));
        var newer = await CreateAdjacentTestContentAsync("adj-newer", AdjacentBaseTime.AddDays(2));

        var result = await _contentPublicAppService.GetAdjacentAsync(anchor.Id, new GetAdjacentContentsInput());

        result.Previous.ShouldNotBeNull();
        result.Previous.Id.ShouldBe(older.Id);
        result.Previous.Url.ShouldEndWith("/blog/adj-older");

        result.Next.ShouldNotBeNull();
        result.Next.Id.ShouldBe(newer.Id);
        result.Next.Url.ShouldEndWith("/blog/adj-newer");
    }

    [Fact]
    public async Task GetAdjacent_Should_Return_Null_Past_Either_End()
    {
        var oldest = await CreateAdjacentTestContentAsync("adj-oldest", AdjacentBaseTime);
        var newest = await CreateAdjacentTestContentAsync("adj-newest", AdjacentBaseTime.AddDays(1));

        (await _contentPublicAppService.GetAdjacentAsync(oldest.Id, new GetAdjacentContentsInput()))
            .Previous.ShouldBeNull();
        (await _contentPublicAppService.GetAdjacentAsync(newest.Id, new GetAdjacentContentsInput()))
            .Next.ShouldBeNull();
    }

    /// <summary>
    /// Comparing PublishTime alone would skip a tied content (strict) or bounce between two of them
    /// (inclusive). Walking Next from the oldest and Previous from the newest must each visit every tied
    /// content exactly once, in the same order the default list shows them.
    /// </summary>
    [Fact]
    public async Task GetAdjacent_Should_Step_Through_Contents_Sharing_A_PublishTime_In_List_Order()
    {
        await CreateAdjacentTestContentAsync("adj-tie-1", AdjacentBaseTime);
        await CreateAdjacentTestContentAsync("adj-tie-2", AdjacentBaseTime, contentTypeId: SiteTestData.PostGalleryTypeId);
        await CreateAdjacentTestContentAsync("adj-tie-3", AdjacentBaseTime);
        await CreateAdjacentTestContentAsync("adj-after-tie", AdjacentBaseTime.AddDays(1));

        var listed = (await _contentPublicAppService.GetListAsync(new GetContentListInput
        {
            PageId = SiteTestData.BlogPageId,
            CultureName = AdjacentCulture,
            MaxResultCount = 1000
        })).Items.Select(c => c.Id).ToList(); // newest first
        listed.Count.ShouldBe(4);

        var forward = new List<Guid> { listed[^1] };
        for (var next = await NextOfAsync(listed[^1]); next != null; next = await NextOfAsync(next.Value))
        {
            forward.Count.ShouldBeLessThan(listed.Count, "walking Next looped");
            forward.Add(next.Value);
        }

        var backward = new List<Guid> { listed[0] };
        for (var previous = await PreviousOfAsync(listed[0]); previous != null; previous = await PreviousOfAsync(previous.Value))
        {
            backward.Count.ShouldBeLessThan(listed.Count, "walking Previous looped");
            backward.Add(previous.Value);
        }

        forward.ShouldBe(Enumerable.Reverse(listed).ToList());
        backward.ShouldBe(listed);
    }

    /// <summary>
    /// The links lead into the list, so they skip whatever the list would not show: a draft, an archived
    /// content, and a scheduled one whose PublishTime has not arrived.
    /// </summary>
    [Fact]
    public async Task GetAdjacent_Should_Skip_Draft_Archived_And_Scheduled_Neighbors()
    {
        var now = AdjacentBaseTime.AddDays(30);
        GetRequiredService<TestClock>().Set(now);

        var older = await CreateAdjacentTestContentAsync("adj-live-older", AdjacentBaseTime);
        await CreateAdjacentTestContentAsync("adj-draft", AdjacentBaseTime.AddDays(1), ContentStatus.Draft);
        await CreateAdjacentTestContentAsync("adj-archived", AdjacentBaseTime.AddDays(2), ContentStatus.Archived);
        var anchor = await CreateAdjacentTestContentAsync("adj-live-anchor", AdjacentBaseTime.AddDays(3));
        await CreateAdjacentTestContentAsync("adj-scheduled", now.AddDays(10));

        var result = await _contentPublicAppService.GetAdjacentAsync(anchor.Id, new GetAdjacentContentsInput());

        result.Previous.ShouldNotBeNull();
        result.Previous.Id.ShouldBe(older.Id);
        result.Next.ShouldBeNull();
    }

    [Fact]
    public async Task GetAdjacent_Should_Keep_To_The_Anchors_Own_Page_And_Language()
    {
        // The About page's single content in the same language, older - a different page.
        await CreateAdjacentTestContentAsync("", AdjacentBaseTime, contentTypeId: SiteTestData.AboutTypeId);
        var anchor = await CreateAdjacentTestContentAsync("adj-alone", AdjacentBaseTime.AddDays(1));
        // The same page, newer - a different language.
        await CreateAdjacentTestContentAsync("adj-other-culture", AdjacentBaseTime.AddDays(2), culture: "de");

        var result = await _contentPublicAppService.GetAdjacentAsync(anchor.Id, new GetAdjacentContentsInput());

        result.Previous.ShouldBeNull();
        result.Next.ShouldBeNull();
    }

    [Fact]
    public async Task GetAdjacent_Should_Span_Every_Content_Type_Unless_Narrowed_To_One()
    {
        var olderArticle = await CreateAdjacentTestContentAsync("adj-article-1", AdjacentBaseTime);
        var olderGallery = await CreateAdjacentTestContentAsync(
            "adj-gallery-1", AdjacentBaseTime.AddDays(1), contentTypeId: SiteTestData.PostGalleryTypeId);
        var anchor = await CreateAdjacentTestContentAsync("adj-article-2", AdjacentBaseTime.AddDays(2));
        var newerGallery = await CreateAdjacentTestContentAsync(
            "adj-gallery-2", AdjacentBaseTime.AddDays(3), contentTypeId: SiteTestData.PostGalleryTypeId);
        var newerArticle = await CreateAdjacentTestContentAsync("adj-article-3", AdjacentBaseTime.AddDays(4));

        var wholePage = await _contentPublicAppService.GetAdjacentAsync(anchor.Id, new GetAdjacentContentsInput());
        wholePage.Previous!.Id.ShouldBe(olderGallery.Id);
        wholePage.Next!.Id.ShouldBe(newerGallery.Id);

        var articlesOnly = await _contentPublicAppService.GetAdjacentAsync(
            anchor.Id, new GetAdjacentContentsInput { ContentTypeId = SiteTestData.PostArticleTypeId });
        articlesOnly.Previous!.Id.ShouldBe(olderArticle.Id);
        articlesOnly.Next!.Id.ShouldBe(newerArticle.Id);
    }

    /// <summary>An archived detail page still answers (<see cref="Content.IsPubliclyAccessible"/>), so its links must too.</summary>
    [Fact]
    public async Task GetAdjacent_Should_Answer_For_An_Archived_Anchor()
    {
        var older = await CreateAdjacentTestContentAsync("adj-around-older", AdjacentBaseTime);
        var anchor = await CreateAdjacentTestContentAsync(
            "adj-archived-anchor", AdjacentBaseTime.AddDays(1), ContentStatus.Archived);
        var newer = await CreateAdjacentTestContentAsync("adj-around-newer", AdjacentBaseTime.AddDays(2));

        var result = await _contentPublicAppService.GetAdjacentAsync(anchor.Id, new GetAdjacentContentsInput());

        result.Previous!.Id.ShouldBe(older.Id);
        result.Next!.Id.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task GetAdjacent_Should_Not_Answer_For_A_Draft_Or_Not_Yet_Due_Anchor()
    {
        var now = AdjacentBaseTime.AddDays(30);
        GetRequiredService<TestClock>().Set(now);

        var draft = await CreateAdjacentTestContentAsync("adj-draft-anchor", AdjacentBaseTime, ContentStatus.Draft);
        var scheduled = await CreateAdjacentTestContentAsync("adj-scheduled-anchor", now.AddDays(1));

        await Should.ThrowAsync<EntityNotFoundException>(
            () => _contentPublicAppService.GetAdjacentAsync(draft.Id, new GetAdjacentContentsInput()));
        await Should.ThrowAsync<EntityNotFoundException>(
            () => _contentPublicAppService.GetAdjacentAsync(scheduled.Id, new GetAdjacentContentsInput()));
    }

    private Task<ContentDto> CreateAdjacentTestContentAsync(
        string slug,
        DateTime publishTime,
        ContentStatus status = ContentStatus.Published,
        Guid? contentTypeId = null,
        string culture = AdjacentCulture)
    {
        return _contentAdminAppService.CreateAsync(new CreateContentDto
        {
            ContentTypeId = contentTypeId ?? SiteTestData.PostArticleTypeId,
            CultureName = culture,
            Slug = slug,
            PublishTime = publishTime,
            Status = status,
            FieldValues = new Dictionary<string, object?> { ["title"] = slug }
        });
    }

    private async Task<Guid?> NextOfAsync(Guid id)
    {
        return (await _contentPublicAppService.GetAdjacentAsync(id, new GetAdjacentContentsInput())).Next?.Id;
    }

    private async Task<Guid?> PreviousOfAsync(Guid id)
    {
        return (await _contentPublicAppService.GetAdjacentAsync(id, new GetAdjacentContentsInput())).Previous?.Id;
    }
}
