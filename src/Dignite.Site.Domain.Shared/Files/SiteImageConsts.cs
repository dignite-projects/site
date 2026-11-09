namespace Dignite.Site.Files;

public static class SiteImageConsts
{
    /// <summary>
    /// The largest side of a picture Site stores and renders, in pixels: the box an upload to
    /// <see cref="SiteFileContainerNames.Images"/> is shrunk to fit, and the largest source and target of the public
    /// read endpoint's rendering. One number for both, so every stored picture can be rendered.
    /// </summary>
    public const int MaxDimension = 4096;

    /// <summary>The largest picture, in pixels, the public read endpoint renders: <see cref="MaxDimension"/> squared.</summary>
    public const long MaxPixelCount = (long)MaxDimension * MaxDimension;
}
