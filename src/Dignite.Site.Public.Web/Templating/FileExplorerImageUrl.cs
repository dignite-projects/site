using System;

namespace Dignite.Site.Public.Templating;

/// <summary>
/// Addresses of FileExplorer images at the size a template lays them out. FileExplorer resizes an image
/// on request (<c>?Width=&amp;Height=</c>), so a field stores the plain address and each template asks for
/// the size it needs.
/// </summary>
public static class FileExplorerImageUrl
{
    /// <summary>
    /// <paramref name="url"/> resized to <paramref name="width"/> (and <paramref name="height"/>, when
    /// given). An address that is not a FileExplorer file - an external image, say - is returned as is,
    /// and so is a blank one.
    /// </summary>
    public static string? Sized(string? url, int width, int? height = null)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.Contains("/file-explorer/files/", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var baseUrl = url.Split('?')[0];
        return height == null
            ? $"{baseUrl}?Width={width}"
            : $"{baseUrl}?Width={width}&Height={height}";
    }
}
