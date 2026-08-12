using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Contents;
using Dignite.Site.Mcp.Errors;
using Shouldly;
using Volo.Abp.Timing;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// The MCP write path, exercised the way an AI client drives it: names instead of ids, and the three
/// footguns 总体设计 §6.2.4 calls out.
/// </summary>
public class ContentTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ContentTools _contentTools;
    private readonly IContentRepository _contentRepository;

    public ContentTools_Tests()
    {
        _contentTools = GetRequiredService<ContentTools>();
        _contentRepository = GetRequiredService<IContentRepository>();
    }

    [Fact]
    public async Task Should_Create_A_Content_Addressed_Entirely_By_Name()
    {
        var created = await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-article",
            cultureName: SiteTestData.EnglishCulture,
            slug: "mcp-created",
            fieldValues: new Dictionary<string, object?> { ["title"] = "Written over MCP" },
            status: ContentStatus.Published,
            publishTime: SiteTestData.PublishTime);

        created.PageId.ShouldBe(SiteTestData.BlogPageId);
        created.ContentTypeId.ShouldBe(SiteTestData.PostArticleTypeId);
        created.Slug.ShouldBe("mcp-created");
        JsonSerializer.Serialize(created.FieldValues["title"]).ShouldBe("\"Written over MCP\"");
    }

    /// <summary>
    /// An explicitly empty slug is a legitimate value, not a missing argument: it names the single content
    /// of a page, whose URL is the page's own route (总体设计 §2.4).
    /// </summary>
    [Fact]
    public async Task Should_Accept_An_Explicitly_Empty_Slug_As_The_Pages_Own_Content()
    {
        var created = await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-gallery",
            cultureName: SiteTestData.ChineseCulture,
            slug: "",
            fieldValues: new Dictionary<string, object?> { ["title"] = "博客首页" },
            publishTime: SiteTestData.PublishTime);

        created.Slug.ShouldBe(string.Empty);

        (await _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.ChineseCulture, string.Empty)).ShouldNotBeNull();
    }

    /// <summary>A model hands over a title as often as a slug, so the tool layer shapes it.</summary>
    [Fact]
    public async Task Should_Normalize_A_Title_Shaped_Slug()
    {
        var created = await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-article",
            cultureName: SiteTestData.EnglishCulture,
            slug: "My Trip To Iceland!",
            fieldValues: new Dictionary<string, object?> { ["title"] = "My trip to Iceland" },
            publishTime: SiteTestData.PublishTime);

        created.Slug.ShouldBe("my-trip-to-iceland");
    }

    /// <summary>
    /// The distinction <c>SlugNormalizer.TryNormalize</c> exists for. Input that normalizes away entirely
    /// must fail loudly - accepting the empty result would be indistinguishable from a deliberate empty
    /// slug and would silently claim the page's own URL.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Slug_That_Normalizes_To_Nothing_Rather_Than_Claiming_The_Pages_Url()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "blog",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: "?!?",
                fieldValues: new Dictionary<string, object?> { ["title"] = "Punctuation only" },
                publishTime: SiteTestData.PublishTime));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("slug");

        // And nothing was written under the empty slug.
        (await _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, string.Empty)).ShouldBeNull();
    }

    /// <summary>
    /// The tool passes the culture through untouched and <c>ContentManager</c> canonicalizes it - one
    /// normalization point, not two (总体设计 §2.4, §6.2.4 second footgun).
    /// </summary>
    [Fact]
    public async Task Should_Store_A_Loosely_Cased_Culture_In_Its_Canonical_Form()
    {
        var created = await _contentTools.CreateContentAsync(
            page: "news",
            contentType: "news-item",
            cultureName: "zh-hans",
            slug: "canonical-culture",
            fieldValues: new Dictionary<string, object?> { ["title"] = "语言标签" },
            publishTime: SiteTestData.PublishTime);

        created.CultureName.ShouldBe(SiteTestData.ChineseCulture);
    }

    /// <summary>
    /// ...and the read side has to canonicalize the address too, or a client that wrote with <c>zh-hans</c>
    /// could not read back what it just created.
    /// </summary>
    [Fact]
    public async Task Should_Find_A_Content_Again_Through_The_Same_Loosely_Cased_Culture()
    {
        await _contentTools.CreateContentAsync(
            page: "news",
            contentType: "news-item",
            cultureName: "zh-hans",
            slug: "round-trip",
            fieldValues: new Dictionary<string, object?> { ["title"] = "回读" },
            publishTime: SiteTestData.PublishTime);

        var read = await _contentTools.GetContentAsync("news", "zh-hans", "round-trip");

        read.CultureName.ShouldBe(SiteTestData.ChineseCulture);
        JsonSerializer.Serialize(read.FieldValues["title"]).ShouldBe("\"\\u56DE\\u8BFB\"");
    }

    [Fact]
    public async Task Should_Update_Only_What_Was_Supplied()
    {
        var updated = await _contentTools.UpdateContentAsync(
            page: "blog",
            cultureName: SiteTestData.EnglishCulture,
            slug: SiteTestData.DraftSlug,
            status: ContentStatus.Published);

        updated.Status.ShouldBe(ContentStatus.Published);
        updated.Slug.ShouldBe(SiteTestData.DraftSlug);
        // fieldValues was omitted, so the body it was seeded with is untouched.
        JsonSerializer.Serialize(updated.FieldValues["title"]).ShouldBe("\"Draft post\"");
    }

    /// <summary>
    /// The write path normalizes the slug and the tool's own description invites a title, so the obvious
    /// thing for a client to do - echo back the string it passed to create - has to work. Otherwise the
    /// content it just made looks like it vanished, and the likely recovery is creating it twice.
    /// </summary>
    [Fact]
    public async Task Should_Address_A_Content_By_The_Same_Unnormalized_String_That_Created_It()
    {
        await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-article",
            cultureName: SiteTestData.EnglishCulture,
            slug: "My Trip To Iceland!",
            fieldValues: new Dictionary<string, object?> { ["title"] = "My trip to Iceland" },
            publishTime: SiteTestData.PublishTime);

        var read = await _contentTools.GetContentAsync("blog", SiteTestData.EnglishCulture, "My Trip To Iceland!");

        read.Slug.ShouldBe("my-trip-to-iceland");

        // ...and the stored form keeps working too, which is the form list_contents hands back.
        (await _contentTools.GetContentAsync("blog", SiteTestData.EnglishCulture, "my-trip-to-iceland"))
            .Id.ShouldBe(read.Id);
    }

    /// <summary>
    /// A slug stored verbatim through the admin API - which does not normalize - must stay reachable.
    /// The raw address is tried first precisely so normalization cannot shadow it.
    /// </summary>
    [Fact]
    public async Task Should_Still_Address_A_Content_Whose_Stored_Slug_Was_Never_Normalized()
    {
        await CreateVerbatimAsync("Legacy-Slug", "Written before MCP");

        var read = await _contentTools.GetContentAsync("blog", SiteTestData.EnglishCulture, "Legacy-Slug");

        read.Slug.ShouldBe("Legacy-Slug");
    }

    /// <summary>
    /// The ordering, pinned properly: with BOTH forms stored under the same page and language - reachable
    /// in practice, since the admin API stores slugs verbatim while create_content normalizes - the raw
    /// address must win. Storing only one form would leave this passing whichever order the lookups ran in,
    /// while a swap would silently make get/update/delete operate on the wrong row.
    /// </summary>
    [Fact]
    public async Task Should_Prefer_The_Exact_Slug_Over_Its_Normalized_Form_When_Both_Exist()
    {
        var verbatim = await CreateVerbatimAsync("Legacy-Slug", "The verbatim one");

        var normalized = await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-article",
            cultureName: SiteTestData.EnglishCulture,
            slug: "Legacy-Slug",
            fieldValues: new Dictionary<string, object?> { ["title"] = "The normalized one" },
            publishTime: SiteTestData.PublishTime);

        normalized.Slug.ShouldBe("legacy-slug");
        normalized.Id.ShouldNotBe(verbatim.Id);

        (await _contentTools.GetContentAsync("blog", SiteTestData.EnglishCulture, "Legacy-Slug"))
            .Id.ShouldBe(verbatim.Id, "the exact address must not be shadowed by the normalized one");

        (await _contentTools.GetContentAsync("blog", SiteTestData.EnglishCulture, "legacy-slug"))
            .Id.ShouldBe(normalized.Id);
    }

    /// <summary>Writes through the admin API, which stores a slug exactly as given.</summary>
    private Task<ContentDto> CreateVerbatimAsync(string slug, string title)
    {
        return GetRequiredService<IContentAdminAppService>().CreateAsync(new CreateContentDto
        {
            ContentTypeId = SiteTestData.PostArticleTypeId,
            CultureName = SiteTestData.EnglishCulture,
            Slug = slug,
            PublishTime = SiteTestData.PublishTime,
            FieldValues = new Dictionary<string, object?> { ["title"] = title }
        });
    }

    /// <summary>
    /// `newSlug` is optional and "leave it alone" is spelled by omitting it, so an empty string is a model
    /// reaching for "no change". Accepting it would move a live content onto the page's own URL.
    /// </summary>
    [Fact]
    public async Task Should_Reject_An_Empty_NewSlug_Rather_Than_Moving_The_Content_To_The_Pages_Url()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.UpdateContentAsync(
                page: "blog",
                cultureName: SiteTestData.EnglishCulture,
                slug: SiteTestData.TripSlug,
                newSlug: ""));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("newSlug");

        await AssertTripDidNotMoveAsync();
    }

    /// <summary>
    /// Whitespace takes the other branch - it is not length-zero, so it falls through to normalization and
    /// fails there. The error still has to blame <c>newSlug</c>, because that is the parameter the caller
    /// passed; naming <c>slug</c> would send a self-correcting client to edit one that was fine.
    /// </summary>
    [Fact]
    public async Task Should_Blame_NewSlug_When_A_Replacement_Normalizes_To_Nothing()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.UpdateContentAsync(
                page: "blog",
                cultureName: SiteTestData.EnglishCulture,
                slug: SiteTestData.TripSlug,
                newSlug: "  "));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("newSlug");
        exception.ValidationErrors.Single().MemberNames.ShouldNotContain("slug");

        await AssertTripDidNotMoveAsync();
    }

    /// <summary>The trip content is still at its own slug, and has not claimed the page's URL.</summary>
    private async Task AssertTripDidNotMoveAsync()
    {
        (await _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug)).ShouldNotBeNull();
        (await _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, string.Empty)).ShouldBeNull();
    }

    /// <summary>
    /// A JSON `null` binds straight through the non-nullable declaration. Unguarded it is a
    /// NullReferenceException reported as an opaque internal error; coerced to empty it silently claims
    /// the page's own URL. Neither is acceptable, so it is a validation error naming the field.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Null_Slug_With_A_Field_Level_Error()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "news",
                contentType: "news-item",
                cultureName: SiteTestData.EnglishCulture,
                slug: null!,
                fieldValues: new Dictionary<string, object?> { ["title"] = "Null slug" },
                publishTime: SiteTestData.PublishTime));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("slug");

        (await _contentRepository.FindBySlugAsync(
            SiteTestData.NewsPageId, SiteTestData.EnglishCulture, string.Empty)).ShouldBeNull();
    }

    /// <summary>
    /// The same footgun as the create path's null slug, on the address side: `"slug": null` binds straight
    /// through get_content's non-nullable parameter too. There is no separate guard for it here - this
    /// walks the same call into <c>ContentAdminAppService.FindBySlugAsync</c>'s own null check - but that
    /// has to still produce a field-level error naming 'slug' rather than a NullReferenceException, and
    /// that guarantee is worth pinning at the tool boundary a model actually calls.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Null_Slug_When_Reading_With_A_Field_Level_Error()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.GetContentAsync("blog", SiteTestData.EnglishCulture, null!));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("slug");
    }

    /// <summary>
    /// The destructive twin of the read-path check above. A null slug must fail before
    /// <c>NameResolver.GetContentAsync</c> ever returns a content to delete - not fall back to deleting the
    /// page's own single content, which is what coercing null to empty would do.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Null_Slug_When_Deleting_With_A_Field_Level_Error()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.DeleteContentAsync("blog", SiteTestData.EnglishCulture, null!));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("slug");
    }


    /// <summary>
    /// Naming a content type without its page is a missing argument, not a missing entity - reporting it
    /// as not-found sends the model to the schema, where the name IS listed, and it retries identically.
    /// </summary>
    [Fact]
    public async Task Should_Report_A_Missing_Page_Argument_As_A_Validation_Error_Naming_Page()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.ListContentsAsync(contentType: "post-article"));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("page");
    }

    [Fact]
    public async Task Should_Move_A_Contents_Url_Through_NewSlug()
    {
        var updated = await _contentTools.UpdateContentAsync(
            page: "blog",
            cultureName: SiteTestData.EnglishCulture,
            slug: SiteTestData.GallerySlug,
            newSlug: "Summer Photos 2026");

        updated.Slug.ShouldBe("summer-photos-2026");
    }

    /// <summary>
    /// The interaction the <c>x ?? current.X</c> pattern in UpdateContentAsync creates between Status and
    /// PublishTime. Published with a future PublishTime is a valid, reachable "scheduled" state (总体设计
    /// §2.4 - ContentManager's own doc on CheckPublishSchedule: scheduling for the future IS publishing
    /// with a future time). Reverting that content to Draft while omitting publishTime keeps the stale
    /// future one - "omit = keep it" - and a draft can never carry one, so the domain has to refuse the
    /// combination. The alternative behaviours would both be worse: silently clearing publishTime the
    /// caller never touched breaks "anything left null is kept as it is", and silently letting a
    /// Draft/future-time record through breaks the invariant altogether.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Reverting_A_Scheduled_Content_To_Draft_Without_Also_Clearing_Its_Future_Time()
    {
        var clock = GetRequiredService<IClock>();
        var future = clock.Now.AddDays(30);

        await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-article",
            cultureName: SiteTestData.EnglishCulture,
            slug: "scheduled-post",
            fieldValues: new Dictionary<string, object?> { ["title"] = "Scheduled" },
            status: ContentStatus.Published,
            publishTime: future);

        await Should.ThrowAsync<ContentDraftCannotHaveFuturePublishTimeException>(async () =>
            await _contentTools.UpdateContentAsync(
                page: "blog",
                cultureName: SiteTestData.EnglishCulture,
                slug: "scheduled-post",
                status: ContentStatus.Draft));

        // Rejected before anything was mutated: still published, still scheduled for a future time - not
        // half-turned into an invalid Draft-with-a-future-time record.
        var stillScheduled = await _contentTools.GetContentAsync("blog", SiteTestData.EnglishCulture, "scheduled-post");
        stillScheduled.Status.ShouldBe(ContentStatus.Published);
        stillScheduled.PublishTime.ShouldBeGreaterThan(clock.Now);
    }

    [Fact]
    public async Task Should_Delete_A_Content_By_Name()
    {
        await _contentTools.DeleteContentAsync("blog", SiteTestData.EnglishCulture, SiteTestData.GallerySlug);

        (await _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.GallerySlug)).ShouldBeNull();
    }

    /// <summary>
    /// The failure a name-addressed surface makes most likely. The message has to send the client back to
    /// the schema rather than leaving it to guess again.
    /// </summary>
    [Fact]
    public async Task Should_Report_An_Unknown_Page_Name_With_What_To_Do_About_It()
    {
        var exception = await Should.ThrowAsync<McpEntityNotFoundException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "blogg",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: "typo",
                publishTime: SiteTestData.PublishTime));

        exception.EntityKind.ShouldBe("page");
        exception.Name.ShouldBe("blogg");
        exception.Message.ShouldContain("get_site_schema");
    }

    /// <summary>
    /// A content type name is unique within its page, so the error has to say which page it looked under -
    /// <c>post-article</c> exists, just not beneath <c>/news</c>.
    /// </summary>
    [Fact]
    public async Task Should_Report_An_Unknown_Content_Type_Within_The_Page_It_Looked_Under()
    {
        var exception = await Should.ThrowAsync<McpEntityNotFoundException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "news",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: "wrong-page",
                publishTime: SiteTestData.PublishTime));

        exception.EntityKind.ShouldBe("content type");
        exception.Message.ShouldContain("news");
    }

    /// <summary>
    /// The write gate is <c>ContentManager</c>'s, not the tool's - the tool must not have its own idea of
    /// what a valid content is, and must not let one through either.
    /// </summary>
    [Fact]
    public async Task Should_Let_The_Domain_Reject_A_Missing_Required_Field()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "blog",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: "no-title",
                fieldValues: new Dictionary<string, object?> { ["body"] = "Body without a title" },
                publishTime: SiteTestData.PublishTime));

        // The field's own name, which is also the key the caller used - so a client knows exactly what to
        // add and can retry in one turn.
        exception.ValidationErrors.SelectMany(error => error.MemberNames).ShouldContain("title");
    }

    /// <summary>
    /// A key present in fieldValues with an explicit JSON `null` value - not the dictionary itself being
    /// null - is a different case from the one above, and has to land the same way:
    /// <c>ContentManager.SetFieldValuesAsync</c> only calls <c>content.SetField</c> for a non-null value,
    /// so a null-valued key is dropped exactly like an absent one, not stored as a literal null. A required
    /// field given `null` must therefore be rejected the same way a required field left out entirely is -
    /// naming the field, not a NullReferenceException and not a silently-accepted null value.
    /// </summary>
    [Fact]
    public async Task Should_Reject_An_Explicit_Null_Field_Value_The_Same_Way_As_A_Missing_Field()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "blog",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: "null-title",
                fieldValues: new Dictionary<string, object?> { ["title"] = null, ["body"] = "Body text" },
                publishTime: SiteTestData.PublishTime));

        exception.ValidationErrors.SelectMany(error => error.MemberNames).ShouldContain("title");
    }

    [Fact]
    public async Task Should_List_Contents_Scoped_To_A_Page_By_Name()
    {
        var result = await _contentTools.ListContentsAsync(page: "blog");

        result.Items.ShouldAllBe(content => content.PageId == SiteTestData.BlogPageId);
        result.Items.Select(content => content.Slug)
            .ShouldContain(SiteTestData.TripSlug);
    }

    /// <summary>
    /// Drafts are visible: MCP calls the Admin side because it is an authoring surface (§6.2.5). The
    /// negative half is the part that matters - the seeded draft appears in the UNFILTERED blog list too,
    /// so asserting only its presence would pass even if the status filter were dropped entirely, and a
    /// model told "publish the drafts" would re-publish and re-date live posts.
    /// </summary>
    [Fact]
    public async Task Should_List_Drafts_And_Only_Drafts()
    {
        var result = await _contentTools.ListContentsAsync(page: "blog", status: ContentStatus.Draft);

        var slugs = result.Items.Select(content => content.Slug).ToList();

        slugs.ShouldContain(SiteTestData.DraftSlug);
        slugs.ShouldNotContain(SiteTestData.TripSlug);
        slugs.ShouldNotContain(SiteTestData.GallerySlug);
        result.Items.ShouldAllBe(content => content.Status == ContentStatus.Draft);
    }

    /// <summary>The mirror image, so neither filter direction can silently become a no-op.</summary>
    [Fact]
    public async Task Should_List_Published_And_Only_Published()
    {
        var result = await _contentTools.ListContentsAsync(page: "blog", status: ContentStatus.Published);

        var slugs = result.Items.Select(content => content.Slug).ToList();

        slugs.ShouldContain(SiteTestData.TripSlug);
        slugs.ShouldNotContain(SiteTestData.DraftSlug);
    }

    /// <summary>The language filter, likewise asserted in both directions.</summary>
    [Fact]
    public async Task Should_List_Only_The_Requested_Language()
    {
        var chinese = await _contentTools.ListContentsAsync(cultureName: SiteTestData.ChineseCulture);

        chinese.Items.ShouldNotBeEmpty();
        chinese.Items.ShouldAllBe(content => content.CultureName == SiteTestData.ChineseCulture);
    }

    /// <summary>A loosely cased language still filters, the same way the address path resolves it.</summary>
    [Fact]
    public async Task Should_List_By_A_Loosely_Cased_Language()
    {
        var chinese = await _contentTools.ListContentsAsync(cultureName: "zh-hans");

        chinese.Items.ShouldNotBeEmpty();
        chinese.Items.ShouldAllBe(content => content.CultureName == SiteTestData.ChineseCulture);
    }

    /// <summary>
    /// An unrecognized language must name <c>cultureName</c>, not report a missing content. Reporting the
    /// slug in every machine-readable field sends the model to re-read the schema, where it finds the slug
    /// listed, so it retries identically - while the language was the one wrong argument all along.
    /// </summary>
    [Fact]
    public async Task Should_Blame_CultureName_When_The_Language_Is_Not_A_Tag()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.GetContentAsync("blog", "english", SiteTestData.TripSlug));

        exception.ValidationErrors.Single().MemberNames.ShouldContain("cultureName");
    }

    /// <summary>
    /// The confirmation has to name the row that was actually deleted, not the address the caller passed.
    /// They differ when the normalized fallback resolves: an identical retry after a successful delete
    /// finds the normalized twin of the row just removed, and an echo of the caller's own input would make
    /// those two deletions read identically.
    /// </summary>
    [Fact]
    public async Task Should_Report_The_Slug_It_Actually_Deleted()
    {
        await CreateVerbatimAsync("Legacy-Slug", "The verbatim one");

        await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-article",
            cultureName: SiteTestData.EnglishCulture,
            slug: "Legacy-Slug",
            fieldValues: new Dictionary<string, object?> { ["title"] = "The normalized one" },
            publishTime: SiteTestData.PublishTime);

        var first = await _contentTools.DeleteContentAsync("blog", SiteTestData.EnglishCulture, "Legacy-Slug");
        var second = await _contentTools.DeleteContentAsync("blog", SiteTestData.EnglishCulture, "Legacy-Slug");

        first.ShouldContain("Legacy-Slug");
        second.ShouldContain("legacy-slug");
        first.ShouldNotBe(second, "two different contents were deleted and must not read identically");
    }

    /// <summary>
    /// The same rule <c>get_content</c> already follows, applied to the list. A culture no CultureInfo
    /// recognizes matches nothing this site could have stored, so it is an empty result - never an
    /// ArgumentException, which the error filter can only report as an opaque internal error and which
    /// would reach an HTTP caller as a 500 over a one-token typo.
    /// </summary>
    [Fact]
    public async Task Should_Return_Nothing_For_An_Unrecognized_Language_Rather_Than_Failing()
    {
        var result = await _contentTools.ListContentsAsync(cultureName: "english");

        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    /// <summary>
    /// Bounded on both sides. A one-sided assertion would still pass if the default drifted to
    /// <c>Clock.Now.AddYears(1)</c> - and a "publish this now" call that silently schedules a year out
    /// produces content nobody ever sees. Compared against <c>IClock</c> rather than <c>DateTime.Now</c>,
    /// since a host may set <c>AbpClockOptions.Kind = Utc</c> and the two would then differ by the offset.
    /// </summary>
    [Fact]
    public async Task Should_Default_PublishTime_To_Now_When_Omitted()
    {
        var clock = GetRequiredService<IClock>();
        var before = clock.Now.AddMinutes(-1);

        var created = await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: "post-article",
            cultureName: SiteTestData.EnglishCulture,
            slug: "no-publish-time",
            fieldValues: new Dictionary<string, object?> { ["title"] = "Undated" });

        created.PublishTime.ShouldBeInRange(before, clock.Now.AddMinutes(1));
    }
}
