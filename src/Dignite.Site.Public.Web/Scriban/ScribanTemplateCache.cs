using System;
using Dignite.Site.Templates;
using global::Scriban;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// Parsed templates, so a page view does not re-parse its template and layout every time.
/// <para>
/// Keyed by the version's own id. Versions of any state pass through it - the published ones pages render, and
/// drafts and older versions a template preview renders - which is safe because no version ever changes once
/// saved (publishing another one only changes which id the render set returns), so an entry never has to be
/// invalidated. The id is globally unique - no tenant in the key, and a template deleted and re-created under
/// the same name, whose version numbers start over, can never be served the old one's tree.
/// </para>
/// <para>
/// A private <see cref="MemoryCache"/> rather than the application's <see cref="IMemoryCache"/>: a size limit
/// belongs to the cache instance, and this one has to be bounded - every tenant's templates pass through it -
/// without imposing sizes on, or competing for, a cache the host configures for its own use.
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

    /// <summary>
    /// The parsed <paramref name="version"/>, named <paramref name="name"/> in its error messages
    /// (<c>name(line,column)</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The body does not parse - possible only for a version saved before a syntax rule it breaks existed,
    /// since saving checks the syntax.
    /// </exception>
    public virtual Template GetOrAdd(SiteTemplateVersionDto version, string name)
    {
        var key = (version.Id, name);
        if (_cache.TryGetValue(key, out Template? cached) && cached != null)
        {
            return cached;
        }

        var body = version.Body ?? string.Empty;
        var template = ScribanTemplateSyntax.Parse(body, name);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"The published version {version.Number} of the Scriban template '{name}' does not parse:\n" +
                ScribanTemplateSyntax.FormatErrors(ScribanTemplateSyntax.GetErrors(body)));
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
