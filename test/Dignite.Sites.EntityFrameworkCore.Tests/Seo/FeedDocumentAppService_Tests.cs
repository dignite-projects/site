using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.EntityFrameworkCore;
using Dignite.Sites.Fields;
using Dignite.Sites.Pages;
using Dignite.Sites.Seo;
using Dignite.Sites.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// The feeds a blog page serves, in all three formats (总体设计 §5.9, GitHub issue #31).
/// </summary>
public class FeedDocumentAppService_Tests : SitesEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private readonly IFeedDocumentAppService _feeds;
    private readonly ContentManager _contentManager;
    private readonly ContentTypeManager _contentTypeManager;
    private readonly IFieldRepository _fieldRepository;
    private readonly TestSettingValueProvider _settings;

    public FeedDocumentAppService_Tests()
    {
        _feeds = GetRequiredService<IFeedDocumentAppService>();
        _contentManager = GetRequiredService<ContentManager>();
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
        _settings = GetRequiredService<TestSettingValueProvider>();

        _settings.Set(SitesSettings.PrimaryDomain, BaseUrl);
        _settings.Set(SitesSettings.EnabledLanguages, "en,zh-Hans");
    }

    [Fact]
    public async Task Rss_Should_Carry_The_Pages_Published_Contents()
    {
        var document = await GetAsync("/blog", "en", SiteFeedFormat.Rss);

        document!.ContentType.ShouldBe(FeedConsts.RssContentType);

        var channel = XDocument.Parse(document.Content).Root!.Element("channel")!;
        channel.Element("title")!.Value.ShouldBe("Blog");
        channel.Element("language")!.Value.ShouldBe("en");

        var items = channel.Elements("item").ToList();
        items.Select(i => i.Element("title")!.Value)
            .ShouldBe(new[] { "My trip", "Summer photos" }, ignoreOrder: true);
        items.Select(i => i.Element("link")!.Value)
            .ShouldContain($"{BaseUrl}/blog/my-trip");
    }

    /// <summary>
    /// Titles come from the SEO field when a type pulled it in, and otherwise from the type's first text
    /// field - the platform has no "title" field of its own to ask, and must not hard-code a tenant's
    /// field names (总体设计 §2.4).
    /// </summary>
    [Fact]
    public async Task Titles_And_Summaries_Should_Come_From_The_Types_Text_Fields()
    {
        var document = await GetAsync("/blog", "en", SiteFeedFormat.Rss);

        var items = XDocument.Parse(document!.Content).Root!.Element("channel")!.Elements("item").ToList();

        var trip = items.First(i => i.Element("title")!.Value == "My trip");
        trip.Element("description")!.Value.ShouldBe("Trip body");

        // The gallery post has a title but no body, so it has no description - inventing one from an
        // unrelated field would be worse than leaving it out.
        var gallery = items.First(i => i.Element("title")!.Value == "Summer photos");
        gallery.Element("description").ShouldBeNull();
    }

    [Fact]
    public async Task An_Seo_Meta_Title_Should_Win_Over_A_Text_Field()
    {
        var contentTypeId = await CreateSeoEnabledBlogTypeAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            await _contentManager.CreateAsync(
                contentTypeId, SitesTestData.EnglishCulture, "with-seo", SitesTestData.PublishTime,
                ContentStatus.Published,
                new Dictionary<string, object?>
                {
                    ["title"] = "The field title",
                    [SeoFieldNames.FieldName] = new SeoFieldValue
                    {
                        MetaTitle = "The SEO title",
                        MetaDescription = "The SEO description"
                    }
                });
        });

        var items = XDocument.Parse((await GetAsync("/blog", "en", SiteFeedFormat.Rss))!.Content)
            .Root!.Element("channel")!.Elements("item").ToList();

        var item = items.First(i => i.Element("link")!.Value == $"{BaseUrl}/blog/with-seo");
        item.Element("title")!.Value.ShouldBe("The SEO title");
        item.Element("description")!.Value.ShouldBe("The SEO description");
    }

    /// <summary>
    /// A custom SEO title with no custom description is the common case - the whole point of an SEO title
    /// override is usually to leave the description to auto-derive. The description must come from the
    /// type's next text field (the body), never from the title field's own raw text repeated back.
    /// </summary>
    [Fact]
    public async Task An_Seo_Meta_Title_Alone_Should_Not_Duplicate_Itself_As_The_Description()
    {
        var contentTypeId = await CreateSeoEnabledBlogTypeWithBodyAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            await _contentManager.CreateAsync(
                contentTypeId, SitesTestData.EnglishCulture, "seo-title-only", SitesTestData.PublishTime,
                ContentStatus.Published,
                new Dictionary<string, object?>
                {
                    ["title"] = "The field title",
                    ["body"] = "The real body text",
                    [SeoFieldNames.FieldName] = new SeoFieldValue { MetaTitle = "The SEO title" }
                });
        });

        var items = XDocument.Parse((await GetAsync("/blog", "en", SiteFeedFormat.Rss))!.Content)
            .Root!.Element("channel")!.Elements("item").ToList();

        var item = items.First(i => i.Element("link")!.Value == $"{BaseUrl}/blog/seo-title-only");
        item.Element("title")!.Value.ShouldBe("The SEO title");
        item.Element("description")!.Value.ShouldBe("The real body text");
    }

    [Fact]
    public async Task Drafts_Should_Not_Be_Syndicated()
    {
        var document = await GetAsync("/blog", "en", SiteFeedFormat.Rss);

        document!.Content.ShouldNotContain(SitesTestData.DraftSlug);
    }

    /// <summary>
    /// noindex is an author saying "keep this out of discovery", and a public feed is discovery - so it is
    /// honoured here as well as in the sitemap, rather than read as a search-engine-only instruction.
    /// </summary>
    [Fact]
    public async Task NoIndexed_Contents_Should_Not_Be_Syndicated()
    {
        var contentTypeId = await CreateSeoEnabledBlogTypeAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            await _contentManager.CreateAsync(
                contentTypeId, SitesTestData.EnglishCulture, "hidden", SitesTestData.PublishTime,
                ContentStatus.Published,
                new Dictionary<string, object?>
                {
                    ["title"] = "Hidden",
                    [SeoFieldNames.FieldName] = new SeoFieldValue { NoIndex = true }
                });
        });

        (await GetAsync("/blog", "en", SiteFeedFormat.Rss))!.Content.ShouldNotContain("/blog/hidden");
    }

    /// <summary>
    /// The under-fetch trap: noindex lives in the value bag, not a column, so it cannot be pushed into the
    /// query. A run of noindexed content newer than everything indexable must not make the feed come back
    /// short - <see cref="FeedSource"/> has to keep paging until it genuinely has enough.
    /// </summary>
    [Fact]
    public async Task Should_Page_Past_A_Run_Of_NoIndexed_Content_To_Reach_MaxItems()
    {
        var contentTypeId = await CreateSeoEnabledBlogTypeAsync();

        // More noindexed posts than MaxItems, all newer than a trailing run of indexable ones - a single
        // fixed-size fetch would exhaust its window entirely on noindexed rows before ever reaching them.
        await WithUnitOfWorkAsync(async () =>
        {
            for (var i = 0; i < FeedConsts.MaxItems + 5; i++)
            {
                await _contentManager.CreateAsync(
                    contentTypeId, SitesTestData.EnglishCulture, $"hidden-{i}",
                    new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-i), ContentStatus.Published,
                    new Dictionary<string, object?>
                    {
                        ["title"] = $"Hidden {i}",
                        [SeoFieldNames.FieldName] = new SeoFieldValue { NoIndex = true }
                    });
            }

            for (var i = 0; i < FeedConsts.MaxItems; i++)
            {
                await _contentManager.CreateAsync(
                    contentTypeId, SitesTestData.EnglishCulture, $"visible-{i}",
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-i), ContentStatus.Published,
                    new Dictionary<string, object?> { ["title"] = $"Visible {i}" });
            }
        });

        var items = XDocument.Parse((await GetAsync("/blog", "en", SiteFeedFormat.Rss))!.Content)
            .Root!.Element("channel")!.Elements("item").ToList();

        items.Count.ShouldBe(FeedConsts.MaxItems);
        items.ShouldAllBe(i => !i.Element("title")!.Value.StartsWith("Hidden", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Items_Should_Be_Newest_First()
    {
        await CreateBlogPostAsync("older", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await CreateBlogPostAsync("newer", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var links = XDocument.Parse((await GetAsync("/blog", "en", SiteFeedFormat.Rss))!.Content)
            .Root!.Element("channel")!.Elements("item")
            .Select(i => i.Element("link")!.Value)
            .ToList();

        links.IndexOf($"{BaseUrl}/blog/newer").ShouldBeLessThan(links.IndexOf($"{BaseUrl}/blog/older"));
    }

    [Fact]
    public async Task Atom_Should_Declare_A_Self_Link_And_The_Newest_Item_As_Its_Update_Time()
    {
        var document = await GetAsync("/blog", "en", SiteFeedFormat.Atom);

        document!.ContentType.ShouldBe(FeedConsts.AtomContentType);

        var feed = XDocument.Parse(document.Content).Root!;
        feed.Name.ShouldBe(Atom + "feed");
        feed.Element(Atom + "id")!.Value.ShouldBe($"{BaseUrl}/blog/atom.xml");

        feed.Elements(Atom + "link")
            .Where(l => (string?)l.Attribute("rel") == "self")
            .Select(l => (string?)l.Attribute("href"))
            .ShouldContain($"{BaseUrl}/blog/atom.xml");

        // The feed's own <updated> is the newest thing in it - not the moment it was generated.
        var newestEntry = feed.Elements(Atom + "entry")
            .Max(e => DateTimeOffset.Parse(e.Element(Atom + "updated")!.Value));
        DateTimeOffset.Parse(feed.Element(Atom + "updated")!.Value).ShouldBe(newestEntry);
    }

    [Fact]
    public async Task Json_Feed_Should_Declare_Version_1_1()
    {
        var document = await GetAsync("/blog", "en", SiteFeedFormat.Json);

        document!.ContentType.ShouldBe(FeedConsts.JsonContentType);

        using var json = JsonDocument.Parse(document.Content);
        var root = json.RootElement;

        root.GetProperty("version").GetString().ShouldBe(FeedConsts.JsonFeedVersion);
        root.GetProperty("feed_url").GetString().ShouldBe($"{BaseUrl}/blog/feed.json");
        root.GetProperty("title").GetString().ShouldBe("Blog");

        var items = root.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(2);

        // JSON Feed requires each item to carry content_text or content_html.
        items.ShouldAllBe(i => i.GetProperty("content_text").GetString()!.Length > 0);
        items.Select(i => i.GetProperty("id").GetString()).ShouldContain($"{BaseUrl}/blog/my-trip");
    }

    [Fact]
    public async Task An_Unknown_Page_Should_Have_No_Feed()
    {
        (await GetAsync("/nope", "en", SiteFeedFormat.Rss)).ShouldBeNull();
    }

    [Fact]
    public async Task An_Unknown_Culture_Should_Have_No_Feed()
    {
        (await GetAsync("/blog", "not-a-culture-at-all", SiteFeedFormat.Rss)).ShouldBeNull();
    }

    [Fact]
    public async Task An_Inactive_Page_Should_Have_No_Feed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var pageRepository = GetRequiredService<IPageRepository>();
            var pageManager = GetRequiredService<PageManager>();
            var blog = await pageRepository.GetAsync(SitesTestData.BlogPageId);
            await pageManager.UpdateAsync(blog, blog.Name, blog.DisplayName, blog.Route, isActive: false);
        });

        (await GetAsync("/blog", "en", SiteFeedFormat.Rss)).ShouldBeNull();
    }

    [Theory]
    [InlineData("blog/feed.xml", FeedConsts.RssContentType)]
    [InlineData("blog/atom.xml", FeedConsts.AtomContentType)]
    [InlineData("blog/feed.json", FeedConsts.JsonContentType)]
    [InlineData("/blog/feed.xml", FeedConsts.RssContentType)]
    public async Task A_Request_Path_Should_Resolve_To_The_Right_Format(string feedPath, string expectedContentType)
    {
        var document = await WithUnitOfWorkAsync(() => _feeds.ResolveAsync(feedPath));

        document.ShouldNotBeNull();
        document!.ContentType.ShouldBe(expectedContentType);
    }

    /// <summary>
    /// The language prefix is taken off with the same context that puts it on - so a feed URL the site
    /// advertises resolves back to the page and language it was built from.
    /// </summary>
    [Fact]
    public async Task A_Language_Prefixed_Path_Should_Resolve_To_That_Language()
    {
        var document = await WithUnitOfWorkAsync(() => _feeds.ResolveAsync("zh-Hans/about/feed.xml"));

        document.ShouldNotBeNull();

        var channel = XDocument.Parse(document!.Content).Root!.Element("channel")!;
        channel.Element("language")!.Value.ShouldBe("zh-Hans");
        channel.Elements("item").Single().Element("title")!.Value.ShouldBe("关于我们");
    }

    [Fact]
    public async Task An_Unprefixed_Path_Should_Resolve_To_The_Default_Language()
    {
        var document = await WithUnitOfWorkAsync(() => _feeds.ResolveAsync("about/feed.xml"));

        var channel = XDocument.Parse(document!.Content).Root!.Element("channel")!;
        channel.Element("language")!.Value.ShouldBe("en");
        channel.Elements("item").Single().Element("title")!.Value.ShouldBe("About us");
    }

    [Theory]
    [InlineData("blog/rss.xml")]
    [InlineData("blog")]
    [InlineData("")]
    public async Task A_Path_That_Is_Not_A_Feed_Address_Should_Resolve_To_Nothing(string feedPath)
    {
        (await WithUnitOfWorkAsync(() => _feeds.ResolveAsync(feedPath))).ShouldBeNull();
    }

    private Task<SiteDocument?> GetAsync(string pageRoute, string cultureName, SiteFeedFormat format)
    {
        return WithUnitOfWorkAsync(() => _feeds.GetAsync(pageRoute, cultureName, format));
    }

    private async Task CreateBlogPostAsync(string slug, DateTime publishTime)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _contentManager.CreateAsync(
                SitesTestData.PostArticleTypeId, SitesTestData.EnglishCulture, slug, publishTime,
                ContentStatus.Published,
                new Dictionary<string, object?> { ["title"] = slug });
        });
    }

    private async Task<Guid> CreateSeoEnabledBlogTypeAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            var titleField = await _fieldRepository.GetAsync(SitesTestData.TitleFieldId);

            var contentType = await _contentTypeManager.CreateAsync(
                SitesTestData.BlogPageId, $"seo-{Guid.NewGuid():N}", "SEO type",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, order: 0),
                    new ContentTypeField(seoField!.Id, order: 1)
                });

            return contentType.Id;
        });
    }

    /// <summary>
    /// Same as <see cref="CreateSeoEnabledBlogTypeAsync"/> but with a second text field (body) after
    /// title - needed to tell apart "the title field's own text" from "the field that should supply the
    /// summary" once the title itself comes from SEO instead.
    /// </summary>
    private async Task<Guid> CreateSeoEnabledBlogTypeWithBodyAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            var titleField = await _fieldRepository.GetAsync(SitesTestData.TitleFieldId);
            var bodyField = await _fieldRepository.GetAsync(SitesTestData.BodyFieldId);

            var contentType = await _contentTypeManager.CreateAsync(
                SitesTestData.BlogPageId, $"seo-body-{Guid.NewGuid():N}", "SEO type with body",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, order: 0),
                    new ContentTypeField(bodyField.Id, order: 1),
                    new ContentTypeField(seoField!.Id, order: 2)
                });

            return contentType.Id;
        });
    }
}
