using System;
using System.Linq;
using Dignite.Abp.BlobManagement;

namespace Dignite.Site.Files;

/// <summary>
/// Addresses of Site's files. A file is Blob Management's, and a browser reads it at Blob Management's public address,
/// <c>/blobs/{container}/{blob}</c> (<see cref="BlobManagementPublicUrl"/>), with <c>?__tenant=</c> for a tenant's file:
/// the address Blob Management hands out as the file's <c>url</c>, which a file field, an SEO image, CKEditor's HTML and
/// a template store as it is. Blob Management resizes a picture on request (<c>?width=&amp;height=</c>, cropping to fill
/// when both are given) to the sizes its container allows, so a field stores the plain address and each consumer asks
/// for the size it needs (<see cref="Sized"/>).
/// <para>
/// An address is <b>relative</b>: no host is written into stored data. A page resolves it against its own origin,
/// whose gateway routes <c>/blobs/*</c> to Blob Management; a document that must carry an absolute address
/// (<c>og:image</c>) puts the site's primary domain in front with <see cref="Absolutize"/>.
/// </para>
/// <para>
/// <b>Earlier addresses.</b> Before 3.0 Site served its files itself at <c>/api/site-public/files/{container}/{blob}</c>
/// (<see cref="RoutePrefix"/>), and stored data still holds such addresses. That endpoint now redirects to Blob
/// Management's address of the same file, so they keep working, and <see cref="Sized"/> and <see cref="IsSiteFile"/>
/// recognize them.
/// </para>
/// </summary>
public static class SiteFileUrl
{
    /// <summary>
    /// Where Site served its files before 3.0, <c>{RoutePrefix}/{containerName}/{blobName}</c>: now a redirect to Blob
    /// Management's address of the file.
    /// </summary>
    public const string RoutePrefix = "api/site-public/files";

    /// <summary>The path segment every earlier Site file address contains, whatever host it is served from.</summary>
    private const string PathMarker = "/site-public/files/";

    /// <summary>
    /// Where File Explorer served the same files before Site took them over (<c>/api/file-explorer/files/</c>), as
    /// some stored addresses still read.
    /// </summary>
    private const string LegacyPathMarker = "/file-explorer/files/";

    /// <summary>
    /// The path Blob Management's public read is served at, on whatever host it is reached. It is recognized only at
    /// the start of an address's path: <c>/blobs/</c> inside another path is no file of Blob Management's.
    /// </summary>
    private static readonly string BlobManagementPathPrefix = "/" + BlobManagementPublicUrl.RoutePrefix + "/";

    /// <summary>
    /// <paramref name="url"/> made absolute on <paramref name="baseUrl"/> (e.g. the site's primary domain,
    /// <c>SiteUrlContext.BaseUrl</c>), for the documents that have to carry an absolute address - an
    /// <c>og:image</c>, say. A relative address gets <paramref name="baseUrl"/> put in front of it; one that
    /// already names its scheme (<c>https:</c>, <c>data:</c>, ...) or is protocol-relative (<c>//cdn...</c>)
    /// is returned as is, and so is a blank one.
    /// </summary>
    public static string? Absolutize(string? url, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || IsAbsolute(url))
        {
            return url;
        }

        var root = baseUrl.TrimEnd('/');
        return url.StartsWith('/') ? root + url : root + "/" + url;
    }

    /// <summary>
    /// Whether <paramref name="url"/> names its own scheme or is protocol-relative. Checked by hand rather than
    /// with <see cref="Uri.TryCreate(string, UriKind, out Uri)"/>, which on Linux and macOS reads a rooted path
    /// such as <c>/blobs/...</c> as an absolute <c>file://</c> URI.
    /// </summary>
    private static bool IsAbsolute(string url)
    {
        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        var colon = url.IndexOf(':');
        if (colon <= 0 || !char.IsAsciiLetter(url[0]))
        {
            return false;
        }

        for (var i = 1; i < colon; i++)
        {
            var c = url[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="url"/> addresses one of Site's files (and so can be sized): Blob Management's public
    /// address, relative or on any host, or an earlier Site address - as opposed to an external image.
    /// </summary>
    public static bool IsSiteFile(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
               && (IsBlobManagementAddress(url)
                   || url.Contains(PathMarker, StringComparison.OrdinalIgnoreCase)
                   || url.Contains(LegacyPathMarker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <paramref name="url"/> resized to <paramref name="width"/> and <paramref name="height"/>, each when given: with
    /// both the picture is cropped to fill the box, with one the other follows the picture's proportions, and with
    /// neither the address is returned as it is. A size already on the address is replaced; every other query
    /// parameter - <c>__tenant</c>, say - is kept. An address that is not a Site file - an external image, say - is
    /// returned as is, and so is a blank one.
    /// <para>
    /// Blob Management serves only the sizes its container allows, so one given alone is rounded up to the next of
    /// <see cref="SiteImageSizes.All"/> - the list the host allows on Site's containers - which keeps the picture's
    /// proportions. Both given are written as asked: rounding them apart would change the box's shape.
    /// </para>
    /// </summary>
    public static string? Sized(string? url, int? width = null, int? height = null)
    {
        if (!IsSiteFile(url) || ((width ?? 0) <= 0 && (height ?? 0) <= 0))
        {
            return url;
        }

        if ((width ?? 0) <= 0)
        {
            width = null;
            height = SiteImageSizes.RoundUp(height!.Value);
        }
        else if ((height ?? 0) <= 0)
        {
            height = null;
            width = SiteImageSizes.RoundUp(width.Value);
        }

        var queryStart = url!.IndexOf('?');
        var baseUrl = queryStart < 0 ? url : url[..queryStart];
        var kept = queryStart < 0
            ? Array.Empty<string>()
            : url[(queryStart + 1)..]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(parameter => !IsSizeParameter(parameter))
                .ToArray();

        var size = (width, height) switch
        {
            (not null, not null) => $"width={width}&height={height}",
            (not null, null) => $"width={width}",
            _ => $"height={height}"
        };
        return kept.Length == 0
            ? $"{baseUrl}?{size}"
            : $"{baseUrl}?{string.Join('&', kept)}&{size}";
    }

    private static bool IsSizeParameter(string parameter)
    {
        var name = parameter.Split('=', 2)[0];
        return name.Equals("width", StringComparison.OrdinalIgnoreCase)
               || name.Equals("height", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether <paramref name="url"/> - relative, or absolute on any host - is Blob Management's public address of a
    /// file: its path starts with <c>/blobs/</c> and names something after it.
    /// </summary>
    private static bool IsBlobManagementAddress(string url)
    {
        var pathStart = 0;
        if (!url.StartsWith('/'))
        {
            var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd <= 0)
            {
                return false;
            }

            pathStart = url.IndexOf('/', schemeEnd + 3);
            if (pathStart < 0)
            {
                return false;
            }
        }

        return string.Compare(url, pathStart, BlobManagementPathPrefix, 0, BlobManagementPathPrefix.Length, StringComparison.OrdinalIgnoreCase) == 0
               && url.Length > pathStart + BlobManagementPathPrefix.Length;
    }
}
