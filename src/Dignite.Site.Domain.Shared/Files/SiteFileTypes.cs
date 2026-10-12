using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Site.Files;

/// <summary>
/// The file types Site's file library takes, as one table of extension and MIME type. The containers'
/// upload policy (<c>SiteAdminApplicationModule</c>) allows the <b>MIME types</b>, because
/// <c>Dignite.Abp.BlobStoring.Pipeline</c> checks the type detected from the bytes; the admin API reports the
/// <b>extensions</b> (<c>FileContainerConfigurationDto.AllowedFileTypeNames</c>), because that is what a file
/// input's <c>accept</c> and the MCP tools speak. Both come from this table, so the two cannot drift apart.
/// <para>
/// Each MIME type is the one <c>MimeTypeDetector</c> reports for content of that extension (its extension map,
/// which matches what its signature engine returns for these formats), so an allowed extension is never
/// refused for being what it says it is.
/// </para>
/// <para>
/// SVG, HTML and script types are deliberately absent: reads are public, so a file a browser renders and
/// executes would be stored XSS on the site's own origin. The exact raster types are listed rather than
/// <c>image/*</c>, which would admit <c>image/svg+xml</c>.
/// </para>
/// </summary>
public static class SiteFileTypes
{
    /// <summary>Raster pictures - the whole of <see cref="SiteFileContainerNames.Images"/>.</summary>
    public static IReadOnlyList<SiteFileType> Images { get; } =
    [
        new(".jpg", "image/jpeg"),
        new(".jpeg", "image/jpeg"),
        new(".png", "image/png"),
        new(".gif", "image/gif"),
        new(".webp", "image/webp")
    ];

    /// <summary>Documents and archives, which <see cref="SiteFileContainerNames.Default"/> takes besides <see cref="Images"/>.</summary>
    public static IReadOnlyList<SiteFileType> Documents { get; } =
    [
        new(".pdf", "application/pdf"),
        new(".doc", "application/msword"),
        new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        new(".xls", "application/vnd.ms-excel"),
        new(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        new(".ppt", "application/vnd.ms-powerpoint"),
        new(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
        new(".txt", "text/plain"),
        new(".csv", "text/csv"),
        new(".zip", "application/zip")
    ];

    /// <summary>Every type of the table, <see cref="Images"/> first.</summary>
    public static IReadOnlyList<SiteFileType> All { get; } = [.. Images, .. Documents];

    /// <summary>The distinct MIME types of <paramref name="types"/>, in table order.</summary>
    public static string[] GetMimeTypes(IEnumerable<SiteFileType> types)
    {
        return types.Select(type => type.MimeType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// The extensions of the table whose MIME type <paramref name="mimeTypes"/> allows - an exact type or a
    /// <c>type/*</c> wildcard, compared case-insensitively as the content-type check compares them. A MIME type
    /// the table has no extension for contributes none.
    /// </summary>
    public static string[] GetExtensions(IEnumerable<string>? mimeTypes)
    {
        if (mimeTypes == null)
        {
            return [];
        }

        var allowed = mimeTypes.ToList();
        return All
            .Where(type => allowed.Any(mimeType => Matches(mimeType, type.MimeType)))
            .Select(type => type.Extension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// The canonical extension (with its dot) of <paramref name="mimeType"/>: the extension of the <b>first</b>
    /// row of <see cref="All"/> with that MIME type, compared case-insensitively (<c>image/jpeg</c> is
    /// <c>.jpg</c>, not <c>.jpeg</c>). <c>null</c> when the table has no row for the type.
    /// </summary>
    public static string? GetExtension(string? mimeType)
    {
        return mimeType == null
            ? null
            : All.FirstOrDefault(type => type.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))?.Extension;
    }

    /// <summary>
    /// Whether <paramref name="mimeType"/> is one of the raster <see cref="Images"/> types - the ones the
    /// public read endpoint resizes on request.
    /// </summary>
    public static bool IsImage(string? mimeType)
    {
        return mimeType != null && Images.Any(type => type.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Matches(string allowed, string mimeType)
    {
        return allowed.EndsWith("/*", StringComparison.Ordinal)
            ? mimeType.StartsWith(allowed[..^1], StringComparison.OrdinalIgnoreCase)
            : allowed.Equals(mimeType, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>One row of <see cref="SiteFileTypes"/>: an extension (with its dot) and its MIME type.</summary>
public sealed record SiteFileType(string Extension, string MimeType);
