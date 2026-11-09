namespace Dignite.Site.Files;

public static class SiteImageConsts
{
    /// <summary>
    /// The largest side of a picture Site renders, in pixels: the largest source and target of the public read
    /// endpoint's rendering, and the tallest picture an upload to <see cref="SiteFileContainerNames.Images"/> keeps. Every
    /// stored picture fits it, so every stored picture can be rendered - including those stored when the upload box
    /// was 4096 wide.
    /// </summary>
    public const int MaxDimension = 4096;

    /// <summary>
    /// The widest picture an upload to <see cref="SiteFileContainerNames.Images"/> keeps, in pixels; a wider one is shrunk
    /// to it. Wide enough for a full-width image on most screens, and a 2560-wide picture is about 40% of the bytes of a
    /// 4096-wide one, which keeps what is stored, and what a page that shows it at its stored size downloads, small.
    /// </summary>
    public const int MaxUploadWidth = 2560;

    /// <summary>The largest picture, in pixels, the public read endpoint renders: <see cref="MaxDimension"/> squared.</summary>
    public const long MaxPixelCount = (long)MaxDimension * MaxDimension;
}
