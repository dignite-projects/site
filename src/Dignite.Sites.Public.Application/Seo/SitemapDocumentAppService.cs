using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Sites.Seo;
using X.Web.Sitemap;
using X.Web.Sitemap.Extensions;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// Serializes the URLs <see cref="SitemapEntrySource"/> produces into a sitemap index and its shards
/// (总体设计 §5.2, GitHub issue #15).
/// <para>
/// <b>What X.Web.Sitemap is used for, and what it is not.</b> The library supplies the schema-correct
/// object model (<see cref="Url"/>, <see cref="Sitemap"/>, <see cref="SitemapIndex"/>,
/// <see cref="SitemapInfo"/>) and its serializers, which is the part worth adopting. Its
/// <c>ISitemapGenerator</c> also splits a URL list into shards - but that routine is private and only
/// reachable through an overload that writes files to a directory, and generating on demand there is no
/// directory to write to. So the split is one <c>Chunk</c> call here, which is exactly what the library's
/// own private method does minus the file I/O.
/// </para>
/// </summary>
public class SitemapDocumentAppService : PublicAppService, ISitemapDocumentAppService
{
    public const string XmlContentType = "application/xml";

    protected SitemapEntrySource EntrySource { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    /// <summary>
    /// Virtual so a deployment - or a test that needs more than one shard without inventing ten thousand
    /// URLs - can change it without touching the rest of the generator.
    /// </summary>
    protected virtual int MaxUrlsPerShard => SitemapConsts.MaxUrlsPerShard;

    public SitemapDocumentAppService(SitemapEntrySource entrySource, SiteUrlBuilder urlBuilder)
    {
        EntrySource = entrySource;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<SiteDocument> GetIndexAsync()
    {
        var context = await UrlBuilder.CreateContextAsync();
        var shards = await GetShardsAsync();

        var infos = shards
            .Select((shard, index) => BuildSitemapInfo(
                UrlBuilder.BuildSitemapShardUrl(context, index + 1),
                // The shard's newest real modification time, never today's date. An index whose entries
                // all claim to have changed today teaches a crawler to ignore lastmod site-wide just as
                // effectively as the URLs themselves doing it.
                MaxLastModified(shard)))
            .ToList();

        // An empty site still gets an index, listing a single empty shard: robots.txt points here
        // unconditionally, and a 404 at an advertised sitemap URL is a reported error in Search Console.
        if (infos.Count == 0)
        {
            infos.Add(BuildSitemapInfo(UrlBuilder.BuildSitemapShardUrl(context, 1), null));
        }

        return new SiteDocument(
            XmlContentType,
            new SitemapIndex(infos).ToXml(),
            shards.Count == 0 ? null : shards.SelectMany(s => s).Max(e => e.LastModified));
    }

    public virtual async Task<SiteDocument?> GetShardAsync(int shardNumber)
    {
        if (shardNumber < 1)
        {
            return null;
        }

        var shards = await GetShardsAsync();

        // Shard 1 exists even on an empty site, so that the index never points at a 404.
        if (shardNumber > Math.Max(shards.Count, 1))
        {
            return null;
        }

        var entries = shardNumber <= shards.Count ? shards[shardNumber - 1] : new List<SitemapEntry>();

        var sitemap = new Sitemap(entries.Select(entry => new Url
        {
            Location = entry.Location,
            // Normalized through the clock rather than forced to UTC - X.Web.Sitemap renders this with a
            // "zzz" offset, which on an unspecified-kind value means the server's local offset. See SeoClock.
            TimeStamp = Clock.ToSitemapTimeStamp(entry.LastModified),
            Priority = entry.Priority
            // ChangeFrequency deliberately left null: it is a guess, current crawlers ignore it, and a
            // wrong guess is worse than its absence.
        }));

        return new SiteDocument(
            XmlContentType,
            sitemap.ToXml(),
            entries.Count == 0 ? null : entries.Max(e => e.LastModified));
    }

    protected virtual async Task<List<List<SitemapEntry>>> GetShardsAsync()
    {
        var entries = await EntrySource.GetEntriesAsync();

        return entries
            .Chunk(MaxUrlsPerShard)
            .Select(chunk => chunk.ToList())
            .ToList();
    }

    /// <summary>
    /// A shard's <c>lastmod</c>, as a date - <see cref="SitemapInfo"/> serializes it as
    /// <c>yyyy-MM-dd</c>, so there is no offset to normalize here.
    /// </summary>
    protected virtual DateTime? MaxLastModified(List<SitemapEntry> shard)
    {
        return shard.Count == 0 ? null : shard.Max(e => e.LastModified);
    }

    /// <summary>
    /// <see cref="SitemapInfo"/> stores a missing date as an empty string, and its
    /// <c>[XmlElement("lastmod")]</c> then serializes as <c>&lt;lastmod /&gt;</c> - an empty element where
    /// the schema requires a date, which is a validation error rather than a silent omission. Nulling the
    /// property makes the serializer leave the element out, which is what "no date" should look like.
    /// </summary>
    protected virtual SitemapInfo BuildSitemapInfo(string location, DateTime? lastModified)
    {
        var info = new SitemapInfo(new Uri(location), lastModified);

        if (lastModified == null)
        {
            info.DateLastModified = null!;
        }

        return info;
    }
}
