namespace Dignite.Site.Public.Files;

/// <summary>
/// The size to serve an image at (<c>?Width=&amp;Height=</c>, see <c>SiteFileUrl.Sized</c>). Both given:
/// cropped to fill exactly that size; one given: scaled to it. Ignored for a file that is not an image.
/// </summary>
public class GetFileInput
{
    public int? Width { get; set; }

    public int? Height { get; set; }

    /// <summary>
    /// Whether the caller takes WebP: a JPEG or PNG is then served as WebP (lossy for a JPEG, lossless for a PNG).
    /// Asked at its stored size, the picture stays as stored when its WebP comes out no smaller; a GIF and a WebP are
    /// never converted. The HTTP endpoint also sets it when the request's
    /// <c>Accept</c> header lists <c>image/webp</c>, as every current browser's does for an image.
    /// </summary>
    public bool AcceptWebp { get; set; }
}
