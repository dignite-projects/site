using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Net.Http.Headers;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Caching;
using Volo.Abp.Timing;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Shared plumbing for the standalone per-domain documents: cache, headers, encoding and compression.
/// <para>
/// These are plain MVC controllers with explicit root routes - no <c>[RemoteService]</c>, no <c>[Area]</c>
/// - because <c>/robots.txt</c> and <c>/sitemap.xml</c> are documents on the tenant's own domain that
/// crawlers fetch at fixed, non-negotiable addresses, not part of the module's API surface.
/// </para>
/// </summary>
public abstract class SiteDocumentController : AbpController
{
    protected IDistributedCache<SiteDocumentCacheItem, string> Cache { get; }

    protected SiteDocumentController(IDistributedCache<SiteDocumentCacheItem, string> cache)
    {
        Cache = cache;
    }

    /// <summary>
    /// Serves a document, generating it on a cache miss.
    /// </summary>
    /// <param name="cacheKey">
    /// Distinguishes the documents of one tenant from each other; the tenant itself is already part of the
    /// key ABP composes.
    /// </param>
    /// <param name="maxAgeSeconds">What crawlers and CDNs are told to hold it for.</param>
    /// <param name="compressible">
    /// Whether the body is worth gzipping. See <see cref="TryCompress"/> for why this is decided per
    /// endpoint rather than by turning on response compression for the whole application.
    /// </param>
    protected virtual async Task<IActionResult> ServeAsync(
        string cacheKey,
        Func<Task<SiteDocument?>> factory,
        int maxAgeSeconds,
        bool compressible)
    {
        var cached = await Cache.GetOrAddAsync(
            BuildCacheKey(cacheKey),
            async () =>
            {
                var document = await factory();

                return document == null
                    ? new SiteDocumentCacheItem(string.Empty, string.Empty, null, found: false)
                    : new SiteDocumentCacheItem(document.ContentType, document.Content, document.LastModified, found: true);
            },
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SiteDocumentCacheItem.Duration
            });

        if (cached is not { Found: true })
        {
            return NotFound();
        }

        return Serve(cached, maxAgeSeconds, compressible);
    }

    /// <summary>
    /// The request's host is part of the key, not decoration: with no primary domain configured the base
    /// URL comes from the request itself, so the same tenant reached on two hosts genuinely has two
    /// different documents, and a single cached copy would serve one host the other's URLs.
    /// </summary>
    protected virtual string BuildCacheKey(string cacheKey)
    {
        return $"{Request.Host.Value}|{cacheKey}";
    }

    protected virtual IActionResult Serve(SiteDocumentCacheItem document, int maxAgeSeconds, bool compressible)
    {
        Response.Headers.CacheControl = $"public, max-age={maxAgeSeconds.ToString(CultureInfo.InvariantCulture)}";

        if (document.LastModified.HasValue)
        {
            Response.Headers.LastModified =
                Clock.ToOffset(document.LastModified.Value).ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);
        }

        // No BOM: it is meaningless in an HTTP body whose charset is declared, and some XML parsers
        // choke on one appearing before the declaration.
        var body = new UTF8Encoding(false).GetBytes(document.Content);

        if (compressible)
        {
            Response.Headers.Vary = HeaderNames.AcceptEncoding;

            if (TryCompress(body, out var compressed))
            {
                Response.Headers.ContentEncoding = "gzip";
                body = compressed;
            }
        }

        return File(body, document.ContentType + "; charset=utf-8");
    }

    /// <summary>
    /// Gzips the body when the client asked for it.
    /// <para>
    /// <b>Why not just enable response compression for the whole application.</b> Compressing an
    /// authenticated HTTPS response alongside attacker-influenced input is the BREACH exposure that
    /// ASP.NET Core avoids by leaving HTTPS compression off by default. These particular documents are
    /// public, hold no secrets and are never personalized, so compressing exactly them - and nothing else
    /// - keeps the saving without widening that surface.
    /// </para>
    /// </summary>
    protected virtual bool TryCompress(byte[] body, out byte[] compressed)
    {
        compressed = body;

        var acceptEncoding = Request.Headers.AcceptEncoding.ToString();
        if (acceptEncoding.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(body, 0, body.Length);
        }

        compressed = output.ToArray();
        return true;
    }
}
