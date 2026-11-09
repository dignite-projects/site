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
    /// The format to serve an image in, one of <see cref="SiteFileFormats"/>; empty is <see cref="SiteFileFormats.Auto"/>.
    /// With <see cref="SiteFileFormats.Webp"/> a JPEG is served as lossy WebP and a PNG as lossless WebP; asked at its
    /// stored size, a picture stays as stored when its WebP comes out no smaller. A GIF and a WebP are never converted.
    /// </summary>
    public string? Format { get; set; }
}
