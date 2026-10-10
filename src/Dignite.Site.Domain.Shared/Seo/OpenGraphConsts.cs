namespace Dignite.Site.Seo;

/// <summary>
/// The size <c>og:image</c> is emitted at (GitHub issue #72): 1200x630, the 1.91:1 image Facebook,
/// LinkedIn and X all recommend for a large link preview. Applied only to images from the site's file
/// library, which the public read endpoint crops to exactly this size on request; an external image
/// address is emitted as written.
/// </summary>
public static class OpenGraphConsts
{
    public const int ImageWidth = 1200;

    public const int ImageHeight = 630;
}
