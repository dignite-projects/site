using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Dignite.Site.Themes;

/// <summary>The files a new theme starts with: a default layout and an empty stylesheet, in the recommended folders.</summary>
public static class SiteThemeSkeleton
{
    public const string StylesheetPath = "assets/site.css";

    public const string Layout =
        "<!DOCTYPE html>\n" +
        "<html lang=\"{{ culture }}\">\n" +
        "<head>\n" +
        "    <meta charset=\"utf-8\" />\n" +
        "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n" +
        "    <title>{{ head?.meta_title ?? page.display_name }}</title>\n" +
        "    {{ site_head }}\n" +
        "    <link rel=\"stylesheet\" href=\"{{ asset_url \"" + StylesheetPath + "\" }}\" />\n" +
        "</head>\n" +
        "<body>\n" +
        "{{ body }}\n" +
        "</body>\n" +
        "</html>\n";

    public const string Stylesheet = "/* The site's styles. */\n";

    public static IReadOnlyList<(string Path, string Body)> Files { get; } = new[]
    {
        (SiteThemeConsts.InitialDefaultLayout, Layout),
        (StylesheetPath, Stylesheet)
    };
}

/// <summary>
/// A fingerprint of a working copy: every file's path and content hash, and the default layout setting, hashed
/// together. What the check hands out and a publish must bring back, so that a publish puts live exactly what was
/// checked - a save by someone else in between changes it.
/// </summary>
public static class SiteThemeFingerprint
{
    public static string Compute(IEnumerable<(string Path, string ContentHash)> files, string? defaultLayout)
    {
        var builder = new StringBuilder();
        builder.Append("default-layout:").Append(defaultLayout ?? string.Empty).Append('\n');

        foreach (var (path, hash) in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            builder.Append(path).Append('\0').Append(hash).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}

/// <summary>One file that differs between the working copy and the live release.</summary>
public sealed record SiteThemeChange(string Path, SiteThemeFileStatus Status);

public static class SiteThemeChanges
{
    /// <summary>
    /// The status of every path in either set: added, modified, deleted (in <paramref name="live"/> only) or
    /// unchanged. With no live release every working copy file is added.
    /// </summary>
    public static List<SiteThemeChange> Compare(
        IEnumerable<(string Path, string ContentHash)> working,
        IEnumerable<(string Path, string ContentHash)> live)
    {
        var workingByPath = working.ToDictionary(file => file.Path, file => file.ContentHash, StringComparer.Ordinal);
        var liveByPath = live.ToDictionary(file => file.Path, file => file.ContentHash, StringComparer.Ordinal);

        return workingByPath.Keys
            .Union(liveByPath.Keys, StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new SiteThemeChange(path, (workingByPath.TryGetValue(path, out var w), liveByPath.TryGetValue(path, out var l)) switch
            {
                (true, false) => SiteThemeFileStatus.Added,
                (false, true) => SiteThemeFileStatus.Deleted,
                _ => string.Equals(w, l, StringComparison.Ordinal) ? SiteThemeFileStatus.Unchanged : SiteThemeFileStatus.Modified
            }))
            .ToList();
    }
}

/// <summary>Rewrites literal references in a template's source - what renaming a file does to the files that name it.</summary>
public static class SiteThemeReferenceRewriter
{
    /// <summary>
    /// <paramref name="body"/> with every one of <paramref name="references"/> replaced by <paramref name="newPath"/>,
    /// in the same quotes. The references must come from this very body.
    /// </summary>
    public static string Rewrite(string body, IEnumerable<SiteThemeReference> references, string newPath)
    {
        var builder = new StringBuilder(body);

        // From the end, so that the offsets of the ones still to replace stay valid.
        foreach (var reference in references.OrderByDescending(r => r.Start))
        {
            var quote = body[reference.Start];
            builder.Remove(reference.Start, reference.Length);
            builder.Insert(reference.Start, quote + newPath + quote);
        }

        return builder.ToString();
    }
}
