using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Public.Files;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// The bounds on the public read endpoint's rendering: one rendering per key at a time, at most
/// <see cref="SiteImageRenderingOptions.MaxConcurrentRenders"/> at once, and the result kept.
/// </summary>
public class RenderedImageCache_Tests
{
    private static readonly RenderedImage Rendered = new(new ImageVariant(new byte[] { 1, 2, 3 }, "image/webp"));

    [Fact]
    public async Task Requests_For_One_Key_Share_One_Rendering_And_Its_Result_Is_Kept()
    {
        using var cache = Create(maxConcurrentRenders: 4);
        var renders = 0;
        var release = new TaskCompletionSource();

        async Task<RenderedImage> Render(CancellationToken _)
        {
            Interlocked.Increment(ref renders);
            await release.Task;
            return Rendered;
        }

        var requests = Enumerable.Range(0, 5).Select(_ => cache.GetOrRenderAsync("key", Render, CancellationToken.None)).ToArray();
        release.SetResult();
        var results = await Task.WhenAll(requests);

        renders.ShouldBe(1);
        results.ShouldAllBe(result => ReferenceEquals(result, Rendered));
        (await cache.GetOrRenderAsync("key", Render, CancellationToken.None)).ShouldBeSameAs(Rendered);
        renders.ShouldBe(1);
    }

    [Fact]
    public async Task No_More_Renderings_Run_At_Once_Than_The_Slots()
    {
        using var cache = Create(maxConcurrentRenders: 2);
        var running = 0;
        var peak = 0;
        var release = new TaskCompletionSource();

        async Task<RenderedImage> Render(CancellationToken _)
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            await release.Task;
            Interlocked.Decrement(ref running);
            return Rendered;
        }

        var requests = Enumerable.Range(0, 6).Select(i => cache.GetOrRenderAsync("key" + i, Render, CancellationToken.None)).ToArray();
        await Task.Delay(100);
        peak.ShouldBe(2);
        release.SetResult();
        await Task.WhenAll(requests);

        peak.ShouldBe(2);
    }

    /// <summary>
    /// The request that started a shared rendering goes away: it stops waiting, and a request still waiting for the same
    /// key renders for itself rather than failing with the other's cancellation.
    /// </summary>
    [Fact]
    public async Task A_Request_Outlives_The_One_That_Started_Its_Rendering()
    {
        using var cache = Create(maxConcurrentRenders: 1);
        using var first = new CancellationTokenSource();
        var started = new TaskCompletionSource();

        async Task<RenderedImage> Render(CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Rendered;
        }

        var abandoned = cache.GetOrRenderAsync("key", Render, first.Token);
        await started.Task;
        var waiting = cache.GetOrRenderAsync("key", _ => Task.FromResult(Rendered), CancellationToken.None);

        first.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => abandoned);
        (await waiting).ShouldBeSameAs(Rendered);
    }

    [Fact]
    public async Task A_Failed_Rendering_Is_Not_Kept()
    {
        using var cache = Create(maxConcurrentRenders: 1);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            cache.GetOrRenderAsync("key", _ => throw new InvalidOperationException("corrupt"), CancellationToken.None));

        (await cache.GetOrRenderAsync("key", _ => Task.FromResult(Rendered), CancellationToken.None)).ShouldBeSameAs(Rendered);
    }

    private static RenderedImageCache Create(int maxConcurrentRenders)
    {
        return new RenderedImageCache(Options.Create(new SiteImageRenderingOptions { MaxConcurrentRenders = maxConcurrentRenders }));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
