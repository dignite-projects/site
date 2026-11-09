namespace Dignite.Site.Public.Files;

/// <summary>
/// What to render from a stored picture, decided by <c>FilePublicAppService</c> from the request and the file's
/// recorded type: the size (both <c>null</c> for the stored size) and the encoding. <see cref="ImageVariantRenderer"/>
/// carries it out and decides nothing.
/// </summary>
/// <param name="Width">The width to scale to, or <c>null</c>; with <paramref name="Height"/> too, the picture is cropped to fill the box.</param>
/// <param name="Height">The height to scale to, or <c>null</c>.</param>
/// <param name="Encoding">The encoding of the rendered image.</param>
public record ImageRendition(int? Width, int? Height, ImageRenditionEncoding Encoding)
{
    /// <summary>Whether a size is asked; without one the picture is only re-encoded.</summary>
    public bool Resizes => Width is > 0 || Height is > 0;

    /// <summary>The part of a cache key that tells this rendition from another of the same picture.</summary>
    public string CacheKey => $"{Width}x{Height}:{Encoding}";
}

/// <summary>The encoding of a rendered image.</summary>
public enum ImageRenditionEncoding
{
    /// <summary>The stored picture's own format: JPEG, PNG or WebP (lossy).</summary>
    SourceFormat,

    /// <summary>Lossy WebP, for a photograph.</summary>
    LossyWebp,

    /// <summary>Lossless WebP: every pixel and the transparency kept, for a drawing, a screenshot or a logo.</summary>
    LosslessWebp,

    /// <summary>
    /// JPEG, for a client that cannot read the stored WebP; PNG instead when the picture has transparency, which JPEG
    /// cannot keep.
    /// </summary>
    Jpeg
}

/// <summary>An image rendered from a stored picture: its encoded bytes and their MIME type.</summary>
public record ImageVariant(byte[] Content, string MimeType);
