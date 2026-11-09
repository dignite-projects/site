using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Files;

/// <summary>
/// Bounds the rendering work of the public read endpoint, which is anonymous and takes any size up to
/// <c>SiteImageConsts.MaxDimension</c>:
/// <list type="bullet">
/// <item>A rendered image is kept in memory (<see cref="SiteImageRenderingOptions.CacheSizeLimit"/>,
/// <see cref="SiteImageRenderingOptions.CacheDuration"/>), in a cache of its own: the host's shared
/// <see cref="IMemoryCache"/> may hold unrelated entries and must not decide this one's eviction. A key names an
/// immutable blob, so an entry never goes stale; it only ages out.</item>
/// <item>Requests for the same key while it renders wait for that one rendering instead of starting their own.</item>
/// <item>At most <see cref="SiteImageRenderingOptions.MaxConcurrentRenders"/> renderings run at once; the rest wait
/// for a slot.</item>
/// </list>
/// One per process.
/// </summary>
public class RenderedImageCache : ISingletonDependency, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly SemaphoreSlim _renderSlots;
    private readonly ConcurrentDictionary<string, Lazy<Task<RenderedImage>>> _inFlight = new();

    protected SiteImageRenderingOptions Options { get; }

    public RenderedImageCache(IOptions<SiteImageRenderingOptions> options)
    {
        Options = options.Value;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = Options.CacheSizeLimit });
        _renderSlots = new SemaphoreSlim(Math.Max(1, Options.MaxConcurrentRenders));
    }

    /// <summary>
    /// The image cached under <paramref name="key"/>, or the result of <paramref name="render"/>, run once for every
    /// request that asks for the key while it runs, inside a render slot. A request that gives up stops waiting; the
    /// rendering carries on for the others unless it was the one that started it, in which case a request still
    /// waiting renders again for itself.
    /// </summary>
    public virtual async Task<RenderedImage> GetOrRenderAsync(
        string key,
        Func<CancellationToken, Task<RenderedImage>> render,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (_cache.TryGetValue<RenderedImage>(key, out var cached) && cached != null)
            {
                return cached;
            }

            var rendering = new Lazy<Task<RenderedImage>>(() => RenderInSlotAsync(key, render, cancellationToken));
            var shared = _inFlight.GetOrAdd(key, rendering);
            if (ReferenceEquals(shared, rendering))
            {
                _ = rendering.Value.ContinueWith(
                    _ => _inFlight.TryRemove(new(key, rendering)),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            try
            {
                return await shared.Value.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt == 0)
            {
                // The request that started the shared rendering went away and cancelled it; this one has not.
                _inFlight.TryRemove(new(key, shared));
            }
        }
    }

    protected virtual async Task<RenderedImage> RenderInSlotAsync(
        string key,
        Func<CancellationToken, Task<RenderedImage>> render,
        CancellationToken cancellationToken)
    {
        await _renderSlots.WaitAsync(cancellationToken);
        try
        {
            var rendered = await render(cancellationToken);
            var size = rendered.Variant?.Content.LongLength ?? 0;
            if (size <= Options.MaxCachedImageBytes)
            {
                // An entry that says "serve the stored picture" is cached too, so a PNG whose WebP is no smaller is not
                // encoded again on every request.
                _cache.Set(key, rendered, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = Options.CacheDuration,
                    Size = Math.Max(1, size)
                });
            }

            return rendered;
        }
        finally
        {
            _renderSlots.Release();
        }
    }

    public virtual void Dispose()
    {
        _cache.Dispose();
        _renderSlots.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// What the public read endpoint serves for a rendition: <see cref="Variant"/>, or the stored picture when it is
/// <c>null</c> (a picture that cannot be rendered, or one whose rendering at its stored size is no smaller).
/// </summary>
public record RenderedImage(ImageVariant? Variant)
{
    public static RenderedImage Stored { get; } = new((ImageVariant?)null);
}
