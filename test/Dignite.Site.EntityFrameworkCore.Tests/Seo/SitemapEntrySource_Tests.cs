using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Settings;
using Dignite.Site.Timing;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// Which URLs the sitemap claims, over 总体设计 §2.6's worked example (GitHub issue #15).
/// </summary>
public class SitemapEntrySource_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";

    private readonly SitemapEntrySource _source;
    private readonly ContentManager _contentManager;
    private readonly ContentTypeManager _contentTypeManager;
    private readonly PageManager _pageManager;
    private readonly IFieldRepository _fieldRepository;
    private readonly IContentRepository _contentRepository;
    private readonly TestSettingValueProvider _settings;
    private readonly TestClock _clock;
    private readonly IClock _realClock;
    private readonly ICurrentTenant _currentTenant;

    public SitemapEntrySource_Tests()
    {
        _source = GetRequiredService<SitemapEntrySource>();
        _contentManager = GetRequiredService<ContentManager>();
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _pageManager = GetRequiredService<PageManager>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _settings = GetRequiredService<TestSettingValueProvider>();
        _clock = GetRequiredService<TestClock>();
        _realClock = GetRequiredService<IClock>();
        _currentTenant = GetRequiredService<ICurrentTenant>();

        _settings.Set(SiteSettings.PrimaryDomain, BaseUrl);
        _settings.Set(SiteSettings.EnabledLanguages, "en,zh-Hans");
    }

    [Fact]
    public async Task Should_Emit_Every_Page_Route_And_Published_Content()
    {
        var locations = await GetLocationsAsync();

        locations.ShouldBe(new[]
        {
            // The home page's route and its single empty-slug content are the same URL, folded into one.
            $"{BaseUrl}/",
            $"{BaseUrl}/about",
            $"{BaseUrl}/zh-Hans/about",
            $"{BaseUrl}/blog",
            $"{BaseUrl}/blog/my-trip",
            $"{BaseUrl}/blog/summer-photos",
            $"{BaseUrl}/news",
            // The dated content path pattern, composed by the page itself.
            $"{BaseUrl}/news/2026/07/launch"
        }, ignoreOrder: true);
    }

    /// <summary>
    /// One page's URLs stay together, which is what gives the shards their per-page grouping without the
    /// shard names having to encode a page.
    /// </summary>
    [Fact]
    public async Task Should_Keep_A_Pages_Urls_Contiguous()
    {
        var locations = await GetLocationsAsync();

        var blogUrls = locations.Select((location, index) => (location, index))
            .Where(x => x.location.Contains("/blog"))
            .Select(x => x.index)
            .ToList();

        (blogUrls.Max() - blogUrls.Min()).ShouldBe(blogUrls.Count - 1);
    }

    [Fact]
    public async Task Should_Not_Emit_Drafts()
    {
        (await GetLocationsAsync()).ShouldNotContain($"{BaseUrl}/blog/{SiteTestData.DraftSlug}");
    }

    [Fact]
    public async Task Should_Not_Emit_Archived_Contents()
    {
        await CreateBlogPostAsync("archived-post", ContentStatus.Archived, SiteTestData.PublishTime);

        (await GetLocationsAsync()).ShouldNotContain($"{BaseUrl}/blog/archived-post");
    }

    /// <summary>
    /// Scheduling is Published plus a publish time that has not arrived - the read path keeps it hidden
    /// until then, and the sitemap has to agree rather than advertise a URL that 404s.
    /// </summary>
    [Fact]
    public async Task Should_Not_Emit_A_Content_Scheduled_For_The_Future()
    {
        await CreateBlogPostAsync("tomorrow", ContentStatus.Published, _realClock.Now.AddDays(1));

        (await GetLocationsAsync()).ShouldNotContain($"{BaseUrl}/blog/tomorrow");
    }

    [Fact]
    public async Task Should_Not_Emit_A_NoIndexed_Content()
    {
        var contentTypeId = await CreateSeoEnabledBlogTypeAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, "hidden", SiteTestData.PublishTime,
                ContentStatus.Published,
                new Dictionary<string, object?>
                {
                    ["title"] = "Hidden",
                    [SeoFieldNames.FieldName] = new SeoFieldValue { NoIndex = true }
                });
        });

        var locations = await GetLocationsAsync();

        locations.ShouldNotContain($"{BaseUrl}/blog/hidden");
        // Its neighbours are untouched - exclusion is per URL, not per page.
        locations.ShouldContain($"{BaseUrl}/blog/my-trip");
    }

    /// <summary>
    /// The case that makes the exclusion set necessary rather than a convenience: an empty-slug content is
    /// reached at its page's own route, so marking it noindex has to take that route out too. Excluding
    /// only the content would leave the identical URL behind under the page's own claim.
    /// </summary>
    [Fact]
    public async Task NoIndexing_A_Pages_Single_Content_Should_Remove_The_Page_Url()
    {
        var pageId = await WithUnitOfWorkAsync(async () =>
            (await _pageManager.CreateAsync("legal", "Legal", "/legal")).Id);

        var contentTypeId = await CreateSeoEnabledTypeAsync(pageId);

        await WithUnitOfWorkAsync(async () =>
        {
            await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, "", SiteTestData.PublishTime,
                ContentStatus.Published,
                new Dictionary<string, object?>
                {
                    ["title"] = "Legal",
                    [SeoFieldNames.FieldName] = new SeoFieldValue { NoIndex = true }
                });
        });

        (await GetLocationsAsync()).ShouldNotContain($"{BaseUrl}/legal");
    }

    [Fact]
    public async Task An_Inactive_Page_Should_Contribute_Nothing()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var blog = await GetRequiredService<IPageRepository>().GetAsync(SiteTestData.BlogPageId);
            await _pageManager.UpdateAsync(blog, blog.Name, blog.DisplayName, blog.Route, isActive: false);
        });

        var locations = await GetLocationsAsync();

        locations.ShouldNotContain($"{BaseUrl}/blog");
        locations.ShouldNotContain($"{BaseUrl}/blog/my-trip");
    }

    [Fact]
    public async Task The_Home_Page_Should_Outrank_The_Others()
    {
        var entries = await WithUnitOfWorkAsync(() => _source.GetEntriesAsync());

        Find(entries, $"{BaseUrl}/").Priority.ShouldBe(SitemapConsts.HomePagePriority);
        Find(entries, $"{BaseUrl}/blog").Priority.ShouldBe(SitemapConsts.PagePriority);
        Find(entries, $"{BaseUrl}/blog/my-trip").Priority.ShouldBe(SitemapConsts.ContentPriority);
    }

    [Fact]
    public async Task A_Contents_Lastmod_Should_Be_Its_Own_Modification_Time()
    {
        var expected = await WithUnitOfWorkAsync(async () =>
        {
            var content = await _contentRepository.FindBySlugAsync(
                SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug);
            return ContentModificationTime.Of(content!);
        });

        var entries = await WithUnitOfWorkAsync(() => _source.GetEntriesAsync());

        Find(entries, $"{BaseUrl}/blog/my-trip").LastModified.ShouldBe(expected);
    }

    /// <summary>
    /// A list page genuinely changes when a post appears under it, so it reports the newest thing it
    /// lists rather than the page row's own untouched timestamp.
    /// </summary>
    [Fact]
    public async Task A_Page_Should_Report_The_Newest_Of_What_It_Lists()
    {
        _clock.Set(new DateTime(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        await CreateBlogPostAsync("newest", ContentStatus.Published, new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var entries = await WithUnitOfWorkAsync(() => _source.GetEntriesAsync());

        var newest = Find(entries, $"{BaseUrl}/blog/newest").LastModified;
        Find(entries, $"{BaseUrl}/blog").LastModified.ShouldBe(newest);
    }

    /// <summary>
    /// <b>The footgun this whole design exists for.</b> If lastmod tracks the moment of generation, every
    /// URL claims to have changed today and Google stops trusting the field across the entire sitemap -
    /// the whole site's worth of signal, not just the rows that were wrong. Two runs at two different
    /// "now"s over unchanged data have to agree exactly.
    /// </summary>
    [Fact]
    public async Task Lastmod_Should_Not_Move_When_Nothing_Changed()
    {
        _clock.Set(new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
        var first = await WithUnitOfWorkAsync(() => _source.GetEntriesAsync());

        _clock.Set(new DateTime(2027, 5, 20, 17, 45, 0, DateTimeKind.Utc));
        var second = await WithUnitOfWorkAsync(() => _source.GetEntriesAsync());

        second.Select(e => (e.Location, e.LastModified))
            .ShouldBe(first.Select(e => (e.Location, e.LastModified)));
    }

    /// <summary>
    /// A scheduled publish arriving is not an edit. There is no background job touching rows at publish
    /// time (GitHub issue #6), and the reported date is the stored publish time either way - so a post
    /// going live does not backdate or forward-date itself to the moment a crawler happened to ask.
    /// </summary>
    [Fact]
    public async Task A_Scheduled_Publish_Arriving_Should_Not_Move_Lastmod()
    {
        var publishTime = new DateTime(2027, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        _clock.Set(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        await CreateBlogPostAsync("scheduled", ContentStatus.Published, publishTime);

        // Before its time: not in the sitemap at all.
        (await GetLocationsAsync()).ShouldNotContain($"{BaseUrl}/blog/scheduled");

        _clock.Set(new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var afterArrival = Find(await WithUnitOfWorkAsync(() => _source.GetEntriesAsync()), $"{BaseUrl}/blog/scheduled");

        _clock.Set(new DateTime(2027, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var muchLater = Find(await WithUnitOfWorkAsync(() => _source.GetEntriesAsync()), $"{BaseUrl}/blog/scheduled");

        afterArrival.LastModified.ShouldBe(publishTime);
        muchLater.LastModified.ShouldBe(afterArrival.LastModified);
    }

    /// <summary>
    /// Strict namespace isolation (总体设计 §5.1 原则 3) - and it comes from ABP's data filter rather than
    /// from anything this generator remembers to do, which is why it holds.
    /// </summary>
    [Fact]
    public async Task Another_Tenant_Should_See_None_Of_This()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await WithUnitOfWorkAsync(() => _source.GetEntriesAsync())).ShouldBeEmpty();
        }
    }

    private async Task<List<string>> GetLocationsAsync()
    {
        var entries = await WithUnitOfWorkAsync(() => _source.GetEntriesAsync());
        return entries.Select(e => e.Location).ToList();
    }

    private static SitemapEntry Find(List<SitemapEntry> entries, string location)
    {
        var entry = entries.FirstOrDefault(e => e.Location == location);
        entry.ShouldNotBeNull($"'{location}' should be in the sitemap");
        return entry!;
    }

    private async Task CreateBlogPostAsync(string slug, ContentStatus status, DateTime publishTime)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _contentManager.CreateAsync(
                SiteTestData.PostArticleTypeId, SiteTestData.EnglishCulture, slug, publishTime, status,
                new Dictionary<string, object?> { ["title"] = slug });
        });
    }

    private Task<Guid> CreateSeoEnabledBlogTypeAsync()
    {
        return CreateSeoEnabledTypeAsync(SiteTestData.BlogPageId);
    }

    /// <summary>A content type that pulls in the seeded SEO field, committed in its own unit of work.</summary>
    private async Task<Guid> CreateSeoEnabledTypeAsync(Guid pageId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            var titleField = await _fieldRepository.GetAsync(SiteTestData.TitleFieldId);

            var contentType = await _contentTypeManager.CreateAsync(
                pageId, $"seo-{Guid.NewGuid():N}", "SEO type",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, order: 0),
                    new ContentTypeField(seoField!.Id, order: 1)
                });

            return contentType.Id;
        });
    }
}
