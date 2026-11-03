using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace Dignite.Site.Themes;

/// <summary>
/// What a theme file's path says about it. The rule is the extension: a path whose last segment has one
/// (<c>css/site.css</c>) is a static file, served as it is; a path without one (<c>Events</c>,
/// <c>blog/_sidebar</c>, <c>layouts/default</c>) is a Scriban template. The one extension that makes a template and
/// not a static file is a language (<c>Events.ja</c>, the Japanese version of <c>Events</c>; see
/// <see cref="TryGetLanguageVersion"/>). Directories mean nothing to the program, <c>locales/</c> aside
/// (<see cref="SiteThemeTextFiles"/>) - <c>layouts/</c> and <c>css/</c> are only where a new theme puts its first
/// files - and a leading <c>_</c> is a naming habit for partials, not a rule.
/// <para>
/// Paths are compared ordinally, as stored: trimmed and without a leading slash (<see cref="Normalize"/>), the
/// same normalization <c>Page.SetTemplate</c> applies, so a page's template value and a template's path compare equal.
/// </para>
/// </summary>
public static class SiteThemePaths
{
    private static readonly Regex PathRegex = new(SiteThemeConsts.PathPattern, RegexOptions.CultureInvariant);

    /// <summary>The shape of a language name: a two or three letter language, then subtags (<c>ja</c>, <c>zh-Hant-TW</c>).</summary>
    private static readonly Regex LanguageNameRegex = new(@"^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant);

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

    /// <summary>
    /// Whether the (normalized) <paramref name="path"/> names a static file: its last segment has an extension, and the
    /// extension is not a language (<see cref="IsLanguageVersion"/>).
    /// </summary>
    public static bool IsAsset(string path)
    {
        return GetExtension(path) != null && !IsLanguageVersion(path);
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
        return extension == null || SiteThemeConsts.AssetExtensions.Contains(extension) || IsLanguageVersion(path);
    }

    /// <summary>
    /// Whether the (normalized) <paramref name="path"/> names the version of a template for one language:
    /// <c>Default.ja</c> is the Japanese version of <c>Default</c>. See <see cref="TryGetLanguageVersion"/>.
    /// </summary>
    public static bool IsLanguageVersion(string path)
    {
        return TryGetLanguageVersion(path, out _, out _);
    }

    /// <summary>
    /// Reads <c>{template}.{language}</c>: the last segment has exactly one dot, and what follows it is a language
    /// that is not already a static file's extension. <c>blog/post.zh-Hant</c> is the <c>zh-Hant</c> version of
    /// <c>blog/post</c>; <c>menu.min.js</c> (two dots) and <c>site.css</c> (a static file's extension) are not
    /// versions of anything.
    /// <para>
    /// The language must be one the runtime knows and spelled the way it spells it (<c>zh-Hans</c>, not
    /// <c>zh-hans</c>), because lookups compare the path as written. A file whose "language" is only a typo
    /// (<c>post.tpl</c>) is therefore refused when it is saved, like any other extension no static file may have,
    /// rather than saved and never found.
    /// </para>
    /// </summary>
    public static bool TryGetLanguageVersion(string path, out string templatePath, out string cultureName)
    {
        templatePath = string.Empty;
        cultureName = string.Empty;

        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.IndexOf('.');
        if (dot <= 0 || dot != name.LastIndexOf('.') || dot == name.Length - 1)
        {
            return false;
        }

        var extension = name[(dot + 1)..];
        if (SiteThemeConsts.AssetExtensions.Contains(extension) || !IsLanguageName(extension))
        {
            return false;
        }

        templatePath = path[..^(extension.Length + 1)];
        cultureName = extension;
        return true;
    }

    /// <summary>The path of the <paramref name="cultureName"/> version of the template at <paramref name="templatePath"/>.</summary>
    public static string GetLanguageVersionPath(string templatePath, string cultureName)
    {
        return templatePath + "." + cultureName;
    }

    private static bool IsLanguageName(string name)
    {
        if (!LanguageNameRegex.IsMatch(name))
        {
            return false;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(name);
            return string.Equals(culture.Name, name, StringComparison.Ordinal)
                   && (culture.CultureTypes & CultureTypes.UserCustomCulture) == 0;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
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
