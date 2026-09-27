namespace Dignite.Site.Public.Templating;

/// <summary>
/// Addresses of FileExplorer images at the size a template lays them out. FileExplorer resizes an image
/// on request (<c>?Width=&amp;Height=</c>), so a field stores the plain address and each template asks for
/// the size it needs.
/// <para>
/// The templates' entry point, kept where <c>_ViewImports</c> already brings it into scope; the
/// implementation is <see cref="Dignite.Site.Files.FileExplorerImageUrl"/>, shared with the server-side
/// <c>og:image</c>.
/// </para>
/// </summary>
public static class FileExplorerImageUrl
{
    /// <inheritdoc cref="Dignite.Site.Files.FileExplorerImageUrl.Sized"/>
    public static string? Sized(string? url, int width, int? height = null)
    {
        return Dignite.Site.Files.FileExplorerImageUrl.Sized(url, width, height);
    }
}
