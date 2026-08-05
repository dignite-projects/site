using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dignite.Sites.EntityFrameworkCore;
using Dignite.Sites.Seo;
using Dignite.Sites.Settings;
using Dignite.Sites.Timing;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// The XML the sitemap endpoints actually serve (总体设计 §5.2, GitHub issue #15).
/// </summary>
public class SitemapDocumentAppService_Tests : SitesEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly ISitemapDocumentAppService _sitemap;
    private readonly SmallShardSitemapDocumentAppService _shardedSitemap;
    private readonly SitemapEntrySource _entrySource;
    private readonly TestSettingValueProvider _settings;
    private readonly TestClock _clock;
    private readonly IClock _realClock;

    public SitemapDocumentAppService_Tests()
    {
        _sitemap = GetRequiredService<ISitemapDocumentAppService>();
        _shardedSitemap = GetRequiredService<SmallShardSitemapDocumentAppService>();
        _entrySource = GetRequiredService<SitemapEntrySource>();
        _settings = GetRequiredService<TestSettingValueProvider>();
        _clock = GetRequiredService<TestClock>();
        _realClock = GetRequiredService<IClock>();

        _settings.Set(SitesSettings.PrimaryDomain, BaseUrl);
    }

    [Fact]
    public async Task Index_Should_Be_A_Sitemapindex_Document()
    {
        var document = await WithUnitOfWorkAsync(() => _sitemap.GetIndexAsync());

        document.ContentType.ShouldBe(SitemapDocumentAppService.XmlContentType);

        var root = XDocument.Parse(document.Content).Root!;
        root.Name.ShouldBe(Ns + "sitemapindex");
        root.Elements(Ns + "sitemap").Select(s => s.Element(Ns + "loc")!.Value)
            .ShouldBe(new[] { $"{BaseUrl}/sitemap-1.xml" });
    }

    [Fact]
    public async Task A_Shard_Should_Be_A_Urlset_Document()
    {
        var document = await WithUnitOfWorkAsync(() => _sitemap.GetShardAsync(1));

        document.ShouldNotBeNull();

        var root = XDocument.Parse(document!.Content).Root!;
        root.Name.ShouldBe(Ns + "urlset");
        root.Elements(Ns + "url").Select(u => u.Element(Ns + "loc")!.Value)
            .ShouldContain($"{BaseUrl}/blog/my-trip");
    }

    [Fact]
    public async Task Shards_Past_The_Last_One_Should_Not_Exist()
    {
        (await WithUnitOfWorkAsync(() => _sitemap.GetShardAsync(2))).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _sitemap.GetShardAsync(0))).ShouldBeNull();
    }

    [Fact]
    public async Task Urls_Should_Be_Split_Across_Shards()
    {
        var total = (await WithUnitOfWorkAsync(() => _entrySource.GetEntriesAsync())).Count;
        var expectedShards = (int)Math.Ceiling(total / (double)SmallShardSitemapDocumentAppService.ShardSize);

        expectedShards.ShouldBeGreaterThan(1, "the seeded scenario should not fit in one small shard");

        var index = XDocument.Parse((await WithUnitOfWorkAsync(() => _shardedSitemap.GetIndexAsync())).Content);
        index.Root!.Elements(Ns + "sitemap").Count().ShouldBe(expectedShards);

        // Every URL is in exactly one shard: none dropped at a boundary, none duplicated across one.
        var served = new System.Collections.Generic.List<string>();
        for (var shard = 1; shard <= expectedShards; shard++)
        {
            var number = shard;
            var document = await WithUnitOfWorkAsync(() => _shardedSitemap.GetShardAsync(number));
            served.AddRange(
                XDocument.Parse(document!.Content).Root!
                    .Elements(Ns + "url")
                    .Select(u => u.Element(Ns + "loc")!.Value));
        }

        served.Count.ShouldBe(total);
        served.Distinct().Count().ShouldBe(total);
    }

    /// <summary>
    /// <see cref="X.Web.Sitemap.Url.Priority"/> is a non-nullable double that always serializes, so an
    /// unset value silently publishes <c>&lt;priority&gt;0&lt;/priority&gt;</c> for the whole site - the
    /// lowest ranking hint available, on every URL.
    /// </summary>
    [Fact]
    public async Task Priority_Should_Never_Be_Zero()
    {
        var document = await WithUnitOfWorkAsync(() => _sitemap.GetShardAsync(1));

        var priorities = XDocument.Parse(document!.Content).Root!
            .Elements(Ns + "url")
            .Select(u => double.Parse(u.Element(Ns + "priority")!.Value, CultureInfo.InvariantCulture))
            .ToList();

        priorities.ShouldNotBeEmpty();
        priorities.ShouldAllBe(p => p > 0);
    }

    /// <summary>
    /// The whole point of <c>lastmod</c>: it reports when content changed, not when a crawler asked. A
    /// generation run in 2030 must not stamp 2030 on a site that has not been touched since 2026 - if it
    /// did, Google would discard the field across the entire sitemap.
    /// </summary>
    [Fact]
    public async Task Lastmod_Should_Not_Report_The_Generation_Time()
    {
        var generatedAt = new DateTime(2030, 6, 15, 9, 0, 0, DateTimeKind.Utc);
        _clock.Set(generatedAt);

        var shard = XDocument.Parse((await WithUnitOfWorkAsync(() => _sitemap.GetShardAsync(1)))!.Content);
        var index = XDocument.Parse((await WithUnitOfWorkAsync(() => _sitemap.GetIndexAsync())).Content);

        foreach (var lastMod in shard.Root!.Elements(Ns + "url").Select(u => u.Element(Ns + "lastmod")!.Value))
        {
            DateTimeOffset.Parse(lastMod, CultureInfo.InvariantCulture).Year.ShouldNotBe(2030);
        }

        foreach (var lastMod in index.Root!.Elements(Ns + "sitemap").Select(s => s.Element(Ns + "lastmod")!.Value))
        {
            lastMod.ShouldNotStartWith("2030");
        }
    }

    /// <summary>
    /// The offset comes from the clock rather than from a hard-coded UTC assumption - see
    /// <see cref="SeoClock"/> for what labelling a local time as <c>+00:00</c> would do to every date.
    /// </summary>
    [Fact]
    public async Task Lastmod_Should_Carry_The_Clocks_Offset()
    {
        var entries = await WithUnitOfWorkAsync(() => _entrySource.GetEntriesAsync());
        var expected = entries.First(e => e.Location == $"{BaseUrl}/blog/my-trip").LastModified;

        var document = await WithUnitOfWorkAsync(() => _sitemap.GetShardAsync(1));

        var lastMod = XDocument.Parse(document!.Content).Root!
            .Elements(Ns + "url")
            .First(u => u.Element(Ns + "loc")!.Value == $"{BaseUrl}/blog/my-trip")
            .Element(Ns + "lastmod")!.Value;

        lastMod.ShouldBe(_realClock.ToSitemapTimeStamp(expected).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// robots.txt advertises the index unconditionally, so it has to exist even on a site with nothing in
    /// it - an advertised sitemap that 404s is a reported error in Search Console.
    /// </summary>
    [Fact]
    public async Task An_Empty_Site_Should_Still_Have_An_Index_And_A_First_Shard()
    {
        using (GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Change(Guid.NewGuid()))
        {
            var index = XDocument.Parse((await WithUnitOfWorkAsync(() => _sitemap.GetIndexAsync())).Content);
            index.Root!.Elements(Ns + "sitemap").Count().ShouldBe(1);

            var shard = await WithUnitOfWorkAsync(() => _sitemap.GetShardAsync(1));
            shard.ShouldNotBeNull();
            XDocument.Parse(shard!.Content).Root!.Elements(Ns + "url").ShouldBeEmpty();
        }
    }
}
