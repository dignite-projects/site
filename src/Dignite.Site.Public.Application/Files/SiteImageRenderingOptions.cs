using System;
using SkiaSharp;

namespace Dignite.Site.Public.Files;

/// <summary>
/// How the public read endpoint renders images (<see cref="ImageVariantRenderer"/>) and how much of that work a
/// process takes on (<see cref="RenderedImageCache"/>). Configure it in a module's <c>ConfigureServices</c> with
/// <c>Configure&lt;SiteImageRenderingOptions&gt;</c>.
/// </summary>
public class SiteImageRenderingOptions
{
    /// <summary>
    /// The sampling of a resize. Linear filtering between linear-blended mipmap levels averages the pixels a
    /// downscale drops; SkiaSharp's default, nearest-neighbour, keeps one and turns fine detail into jagged noise.
    /// </summary>
    public SKSamplingOptions Sampling { get; set; } = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>The quality, 0-100, of a JPEG rendered for a caller that does not take WebP.</summary>
    public int JpegQuality { get; set; } = 85;

    /// <summary>
    /// The quality, 0-100, of the lossy WebP a JPEG is rendered as. Not the same scale as <see cref="JpegQuality"/>:
    /// WebP at a given number keeps more detail than JPEG at the same number.
    /// </summary>
    public int WebpQuality { get; set; } = 85;

    /// <summary>
    /// Lossless WebP's effort, 0-100, for a PNG: what libwebp spends to find a smaller encoding of the same pixels;
    /// the image is identical whatever the value. On a 3840 x 2160 PNG, 25 took 0.6 s where libwebp's default of 75
    /// took 3.9 s, for a file 1% larger.
    /// </summary>
    public float LosslessWebpEffort { get; set; } = 25;

    /// <summary>
    /// How many images a process renders at once; a request beyond it waits for a slot. Rendering is CPU-bound and
    /// the endpoint is anonymous, so this keeps a burst of new addresses from taking every core.
    /// </summary>
    public int MaxConcurrentRenders { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>The memory, in bytes, rendered images are kept in.</summary>
    public long CacheSizeLimit { get; set; } = 64 * 1024 * 1024;

    /// <summary>The largest rendered image kept in the cache, in bytes; a larger one is served but rendered again next time.</summary>
    public long MaxCachedImageBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>How long a rendered image is kept. A shared cache in front of the endpoint keeps it longer.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);
}
