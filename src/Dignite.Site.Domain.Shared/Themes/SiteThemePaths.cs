using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace Dignite.Site.Themes;

/// <summary>
/// What a theme file's path says about it. The whole rule is the extension: a path whose last segment has one
/// (<c>assets/site.css</c>) is a static file, served as it is; a path without one (<c>Events</c>,
/// <c>blog/_sidebar</c>, <c>layouts/default</c>) is a Scriban template. Directories mean nothing to the program -
/// <c>layouts/</c> and <c>assets/</c> are only where a new theme puts its first files - and a leading <c>_</c> is a
/// naming habit for partials, not a rule.
/// <para>
/// Paths are compared ordinally, as stored: trimmed and without a leading slash (<see cref="Normalize"/>), the
/// same normalization <c>Page.SetTemplate</c> applies, so a page's template value and a template's path compare equal.
/// </para>
/// </summary>
public static class SiteThemePaths
{
    private static readonly Regex PathRegex = new(SiteThemeConsts.PathPattern, RegexOptions.CultureInvariant);

    /// <summary>The canonical stored form of a path: trimmed, without a leading slash.</summary>
    public static string Normalize(string path)
    {
        Check.NotNull(path, nameof(path));

        return path.Trim().RemovePreFix("/");
    }

    /// <summary>Whether the (normalized) <paramref name="path"/> has the shape and length of a theme file's path.</summary>
    public static bool IsValid(string path)
    {
        return !string.IsNullOrEmpty(path)
               && path.Length <= SiteThemeConsts.MaxPathLength
               && PathRegex.IsMatch(path);
    }

    /// <summary>Whether the (normalized) <paramref name="path"/> names a static file: its last segment has an extension.</summary>
    public static bool IsAsset(string path)
    {
        return GetExtension(path) != null;
    }

    /// <summary>Whether the (normalized) <paramref name="path"/> names a Scriban template: its last segment has no extension.</summary>
    public static bool IsTemplate(string path)
    {
        return !IsAsset(path);
    }

    /// <summary>
    /// The extension of the last segment, without the dot (<c>js</c> for <c>menu.min.js</c>), or null when it has
    /// none.
    /// </summary>
    public static string? GetExtension(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');

        return dot <= 0 || dot == name.Length - 1 ? null : name[(dot + 1)..];
    }

    /// <summary>
    /// Whether a file at <paramref name="path"/> may be saved: a valid path, and either a template or a static file
    /// with an extension in <see cref="SiteThemeConsts.AssetExtensions"/>.
    /// </summary>
    public static bool IsAllowed(string path)
    {
        if (!IsValid(path))
        {
            return false;
        }

        var extension = GetExtension(path);
        return extension == null || SiteThemeConsts.AssetExtensions.Contains(extension);
    }

    /// <summary>
    /// The SHA-256 of <paramref name="body"/>'s UTF-8 bytes as 64 lower-case hex digits: what a file is compared,
    /// deduplicated and cached by, and what <c>asset_url</c> puts in a static file's address.
    /// </summary>
    public static string ComputeContentHash(string body)
    {
        Check.NotNull(body, nameof(body));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
    }
}
