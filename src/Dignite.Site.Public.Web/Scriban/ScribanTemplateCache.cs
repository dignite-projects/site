using System;
using Dignite.Site.Themes;
using global::Scriban;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// Parsed templates, so a page view does not re-parse its templates every time.
/// <para>
/// Keyed by the file's content hash and its path. A content never changes under its hash, so an entry never has to
/// be invalidated: the live release, an older one and the working copy all share an entry for a file they have in
/// common, and saving or publishing simply leads to another key. The path is in the key because a parsed template
/// carries it into its error messages (<c>path(line,column)</c>), and two files with the same content keep their own.
/// </para>
/// <para>
/// A private <see cref="MemoryCache"/> rather than the application's <see cref="IMemoryCache"/>: a size limit belongs
/// to the cache instance, and this one has to be bounded - every tenant's templates pass through it - without imposing
/// sizes on, or competing for, a cache the host configures for its own use.
/// </para>
/// </summary>
public class ScribanTemplateCache : ISingletonDependency, IDisposable
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromHours(1);

    private readonly MemoryCache _cache;

    public ScribanTemplateCache(IOptions<SiteScribanOptions> options)
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = options.Value.TemplateCacheSizeLimit });
    }

    /// <summary>The parsed <paramref name="file"/>, named by its path in its error messages.</summary>
    /// <exception cref="InvalidOperationException">
    /// The body does not parse - possible for a working copy file, which may be saved with errors, and for a release
    /// published before a syntax rule it breaks existed.
    /// </exception>
    public virtual Template GetOrAdd(SiteThemeSnapshotFileDto file)
    {
        var key = (file.ContentHash, file.Path);
        if (_cache.TryGetValue(key, out Template? cached) && cached != null)
        {
            return cached;
        }

        var body = file.Body ?? string.Empty;
        var template = ScribanTemplateSyntax.Parse(body, file.Path);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"The Scriban template '{file.Path}' does not parse:\n" +
                ScribanTemplateSyntax.FormatErrors(ScribanTemplateSyntax.GetErrors(template)));
        }

        // Concurrent first requests may both parse; the result is the same, and the later Set simply wins.
        _cache.Set(key, template, new MemoryCacheEntryOptions
        {
            Size = Math.Max(1, body.Length),
            SlidingExpiration = SlidingExpiration
        });

        return template;
    }

    public void Dispose()
    {
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }
}
