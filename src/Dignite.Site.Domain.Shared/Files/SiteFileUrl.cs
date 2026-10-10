using System;
using System.Linq;

namespace Dignite.Site.Files;

/// <summary>
/// Addresses of Site's files: where the public read endpoint serves them, and the same file at another
/// size. The endpoint resizes an image on request (<c>?Width=&amp;Height=</c>, cropping to fill when both
/// are given), so a field stores the plain address and each consumer asks for the size it needs.
/// <para>
/// Lives here rather than in Public.Web so the server-side <c>og:image</c> (<c>HeadMetadataBuilder</c>),
/// the Razor templates (through <c>Dignite.Site.Public.Templating.SiteFileUrl</c>), the admin API and the
/// MCP tools build and recognize the same addresses.
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
    /// The absolute address of a stored file, on <paramref name="baseUrl"/> (scheme and host, e.g. the
    /// request's). <c>__tenant</c> is always present - empty for the host - so the endpoint resolves the
    /// file's tenant from the address alone, wherever it is embedded.
    /// </summary>
    public static string Build(string baseUrl, string containerName, string blobName, Guid? tenantId)
    {
        return $"{baseUrl.TrimEnd('/')}/{RoutePrefix}/{containerName}/{blobName}?__tenant={tenantId}";
    }

    /// <summary>
    /// Whether <paramref name="url"/> addresses a file Site serves (and so can be sized), as opposed to an
    /// external image.
    /// </summary>
    public static bool IsSiteFile(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
               && (url.Contains(PathMarker, StringComparison.OrdinalIgnoreCase)
                   || url.Contains(LegacyPathMarker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <paramref name="url"/> resized to <paramref name="width"/> (and <paramref name="height"/>, when
    /// given). A size already on the address is replaced; every other query parameter - <c>__tenant</c> in
    /// particular - is kept. An address that is not a Site file - an external image, say - is returned as
    /// is, and so is a blank one.
    /// </summary>
    public static string? Sized(string? url, int width, int? height = null)
    {
        if (!IsSiteFile(url))
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
