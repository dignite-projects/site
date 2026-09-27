using System;
using System.Linq;

namespace Dignite.Site.Files;

/// <summary>
/// Addresses of FileExplorer images at a given size. FileExplorer resizes an image on request
/// (<c>?Width=&amp;Height=</c>, cropping to fill when both are given), so a field stores the plain address
/// and each consumer asks for the size it needs.
/// <para>
/// Lives here rather than in Public.Web so the server-side <c>og:image</c> (<c>HeadMetadataBuilder</c>) and
/// the Razor templates (through <c>Dignite.Site.Public.Templating.FileExplorerImageUrl</c>) size an image
/// the same way.
/// </para>
/// </summary>
public static class FileExplorerImageUrl
{
    /// <summary>
    /// <paramref name="url"/> resized to <paramref name="width"/> (and <paramref name="height"/>, when
    /// given). A size already on the address is replaced; every other query parameter - FileExplorer's own
    /// <c>__tenant</c> in particular - is kept. An address that is not a FileExplorer file - an external
    /// image, say - is returned as is, and so is a blank one.
    /// </summary>
    public static string? Sized(string? url, int width, int? height = null)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.Contains("/file-explorer/files/", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var queryStart = url.IndexOf('?');
        var baseUrl = queryStart < 0 ? url : url[..queryStart];
        var kept = queryStart < 0
            ? Array.Empty<string>()
            : url[(queryStart + 1)..]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(parameter => !IsSizeParameter(parameter))
                .ToArray();

        var size = height == null ? $"Width={width}" : $"Width={width}&Height={height}";
        return kept.Length == 0
            ? $"{baseUrl}?{size}"
            : $"{baseUrl}?{string.Join('&', kept)}&{size}";
    }

    private static bool IsSizeParameter(string parameter)
    {
        var name = parameter.Split('=', 2)[0];
        return name.Equals("Width", StringComparison.OrdinalIgnoreCase)
               || name.Equals("Height", StringComparison.OrdinalIgnoreCase);
    }
}
