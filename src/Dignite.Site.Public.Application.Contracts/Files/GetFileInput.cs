namespace Dignite.Site.Public.Files;

/// <summary>
/// The size to serve an image at (<c>?Width=&amp;Height=</c>, see <c>SiteFileUrl.Sized</c>). Both given:
/// cropped to fill exactly that size; one given: scaled to it. Ignored for a file that is not an image.
/// </summary>
public class GetFileInput
{
    public int? Width { get; set; }

    public int? Height { get; set; }
}
