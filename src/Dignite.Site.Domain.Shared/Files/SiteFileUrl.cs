using System;
using System.Linq;

namespace Dignite.Site.Files;

/// <summary>
/// Addresses of Site's files: where the public read endpoint serves them, and the same file at another
/// size. The endpoint resizes an image on request (<c>?Width=&amp;Height=</c>, cropping to fill when both
/// are given), so a field stores the plain address and each consumer asks for the size it needs.
/// <para>
/// An address is <b>relative</b> - <c>/api/site-public/files/{container}/{blob}</c> - the same as
/// CmsKit's media addresses (<c>/api/cms-kit/media/{id}</c>): no host is written into stored data, so moving
/// the site, its API or its gateway to another host leaves every stored address valid. A page on the public
/// site resolves it against its own origin; a document that must carry an absolute address (<c>og:image</c>)
/// puts the site's primary domain in front with <see cref="Absolutize"/>; the admin UI puts its API's.
/// </para>
/// <para>
/// Lives here rather than in Public.Web so the server-side <c>og:image</c> (<c>HeadMetadataBuilder</c>),
/// the Razor templates (through <c>Dignite.Site.Public.Templating.SiteFileUrl</c>), the admin API and the
/// MCP tools build and recognize the same addresses.
/// </para>
/// <para>
/// <b>Blob Management's addresses.</b> Site's files are Blob Management's, and it hands out its own public address
/// (<c>/blobs/{container}/{blob}?__tenant=...</c>) for a file marked public, which a file field or an SEO image may
/// store. Site serves the same file at its own address, with the format a browser takes and any size, so
/// <see cref="Normalize"/> turns the one into the other, and <see cref="Sized"/>, <see cref="Absolutize"/> and
/// <see cref="IsSiteFile"/> do it first.
/// </para>
/// </summary>
public static class SiteFileUrl
{
    /// <summary>
    /// The public read endpoint, <c>{RoutePrefix}/{containerName}/{blobName}</c>. One definition for every
    /// surface that hands an address out - the admin API, the MCP tools - and for the controller that
    /// serves it.
    /// </summary>
    public const string RoutePrefix = "api/site-public/files";

    /// <summary>
    /// The path segment every Site file address contains, whatever host it is served from.
    /// </summary>
    private const string PathMarker = "/site-public/files/";

    /// <summary>
    /// Where File Explorer served the same files before Site took them over
    /// (<c>/api/file-explorer/files/</c>). Still recognized for one version, 0.1.0-preview.25, so
    /// addresses stored before the host ran the <c>Site_MigrateFileExplorerData</c> data migration keep being
    /// sized; remove it in the release after.
    /// </summary>
    private const string LegacyPathMarker = "/file-explorer/files/";

    /// <summary>
    /// The path Blob Management's public read is served at (<c>/blobs/{container}/{blob}</c>), on whatever host it is
    /// reached. It is recognized only at the start of an address's path: <c>/blobs/</c> inside another path is no file of
    /// Blob Management's.
    /// </summary>
    private const string BlobManagementPathPrefix = "/blobs/";

    /// <summary>The query parameter Blob Management names a file's tenant with; Site's address names none.</summary>
    private const string TenantParameter = "__tenant";

    /// <summary>
    /// The address of a stored file, relative to whichever host serves the site:
    /// <c>/api/site-public/files/{containerName}/{blobName}</c>. It names no tenant: the endpoint finds the
    /// file's tenant the way every other request does (the host name, a header, ...), so the address stays
    /// the same for a site that moves to another tenant or domain. Addresses issued before this carry
    /// <c>?__tenant=</c>, which the endpoint still honours.
    /// </summary>
    public static string Build(string containerName, string blobName)
    {
        return $"/{RoutePrefix}/{containerName}/{blobName}";
    }

    /// <summary>
    /// <paramref name="url"/> made absolute on <paramref name="baseUrl"/> (e.g. the site's primary domain,
    /// <c>SiteUrlContext.BaseUrl</c>), for the documents that have to carry an absolute address - an
    /// <c>og:image</c>, say. A relative address gets <paramref name="baseUrl"/> put in front of it; one that
    /// already names its scheme (<c>https:</c>, <c>data:</c>, ...) or is protocol-relative (<c>//cdn...</c>)
    /// is returned as is, and so is a blank one.
    /// </summary>
    public static string? Absolutize(string? url, string baseUrl)
    {
        url = Normalize(url);
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
    /// such as <c>/api/...</c> as an absolute <c>file://</c> URI.
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
    /// Whether <paramref name="url"/> addresses a file Site serves (and so can be sized), as opposed to an
    /// external image.
    /// </summary>
    public static bool IsSiteFile(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
               && (url.Contains(PathMarker, StringComparison.OrdinalIgnoreCase)
                   || url.Contains(LegacyPathMarker, StringComparison.OrdinalIgnoreCase)
                   || TryGetBlobManagementPath(url, out _));
    }

    /// <summary>
    /// The Site address of a file Blob Management addresses as <paramref name="url"/>: <c>/blobs/{container}/{blob}</c>,
    /// relative or on any host, becomes <c>/api/site-public/files/{container}/{blob}</c>, relative, without Blob
    /// Management's <c>__tenant</c> - the site's request names its tenant - and with every other query parameter kept. Any
    /// other address, a blank one included, is returned as it is.
    /// </summary>
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !TryGetBlobManagementPath(url, out var rest))
        {
            return url;
        }

        var queryStart = rest.IndexOf('?');
        var path = queryStart < 0 ? rest : rest[..queryStart];
        var kept = queryStart < 0
            ? Array.Empty<string>()
            : rest[(queryStart + 1)..]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(parameter => !parameter.Split('=', 2)[0].Equals(TenantParameter, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var address = $"/{RoutePrefix}/{path}";
        return kept.Length == 0 ? address : $"{address}?{string.Join('&', kept)}";
    }

    /// <summary>
    /// Whether <paramref name="url"/> - relative, or absolute on any host - is the public address of a file of Blob
    /// Management (its path starts with <c>/blobs/</c>); <paramref name="rest"/> is what follows that prefix, query
    /// included.
    /// </summary>
    private static bool TryGetBlobManagementPath(string url, out string rest)
    {
        rest = string.Empty;

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

        if (string.Compare(url, pathStart, BlobManagementPathPrefix, 0, BlobManagementPathPrefix.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        rest = url[(pathStart + BlobManagementPathPrefix.Length)..];
        return rest.Length > 0;
    }

    /// <summary>
    /// <paramref name="url"/> resized to <paramref name="width"/> and <paramref name="height"/>, each when
    /// given: with both the picture is cropped to fill the box, with one the other follows the picture's
    /// proportions, and with neither the address is returned as it is. A size already on the address is replaced;
    /// every other query parameter - <c>__tenant</c> on an address issued by an earlier version, say - is kept. An
    /// address that is not a Site file - an external image, say - is returned as is, and so is a blank one.
    /// </summary>
    public static string? Sized(string? url, int? width = null, int? height = null)
    {
        url = Normalize(url);
        if (!IsSiteFile(url) || (width == null && height == null))
        {
            return url;
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
            (not null, not null) => $"Width={width}&Height={height}",
            (not null, null) => $"Width={width}",
            _ => $"Height={height}"
        };
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
