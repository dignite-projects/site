using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Volo.Abp;

namespace Dignite.Site.Themes;

/// <summary>
/// A theme as one zip file, for taking it out of the site and bringing it back (dev-docs/site-theme-import-export.md):
/// each entry is a file of the theme at its own path - templates without an extension, as they are stored - and
/// <see cref="ManifestName"/> holds the default layout. Nothing else: a theme holds no binary file.
/// </summary>
public static class SiteThemePackage
{
    /// <summary>
    /// The entry that holds the default layout and where the package came from. A path segment of a theme file cannot
    /// start with a dot, so no file of the theme is ever named so.
    /// </summary>
    public const string ManifestName = ".site-theme.json";

    /// <summary>The most files a package may hold. A host can change it.</summary>
    public static int MaxFileCount { get; set; } = 1000;

    /// <summary>The most bytes the files of a package may hold once unpacked, and the most a package may be. A host can change it.</summary>
    public static long MaxBytes { get; set; } = 32 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { WriteIndented = true };

    /// <summary>The zip of <paramref name="files"/>, with the default layout and where they come from in the manifest.</summary>
    public static byte[] Write(IEnumerable<KeyValuePair<string, string>> files, string? defaultLayout, string source, DateTime exportedAt)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, body) in files.OrderBy(file => file.Key, StringComparer.Ordinal))
            {
                WriteEntry(archive, path, body);
            }

            var manifest = new JsonObject
            {
                ["defaultLayout"] = defaultLayout,
                ["source"] = source,
                ["exportedAt"] = exportedAt
            };
            WriteEntry(archive, ManifestName, manifest.ToJsonString(ManifestJsonOptions));
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// The files of the package in <paramref name="zip"/>, checked as a save checks a file - the path's shape and
    /// extension, the size of the body - and as a package must be: paths inside the theme only, UTF-8 text, within
    /// <see cref="MaxFileCount"/> and <see cref="MaxBytes"/>. Everything wrong is reported at once.
    /// Hidden files (a segment starting with a dot, such as <c>.DS_Store</c>) and the <c>__MACOSX/</c> folder that a
    /// zip made on a Mac carries are skipped.
    /// </summary>
    /// <exception cref="BusinessException"><see cref="SiteErrorCodes.SiteThemePackageInvalid"/>, with Data["Problems"] listing what is wrong.</exception>
    public static SiteThemePackageContent Read(Stream zip)
    {
        var problems = new List<string>();
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasDefaultLayout = false;
        string? defaultLayout = null;

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw Invalid(new[] { "The package is not a zip file." });
        }

        using (archive)
        {
            long totalBytes = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                if (name.EndsWith('/') || IsSkipped(name))
                {
                    continue;
                }

                if (name.Contains('\\') || name.StartsWith('/') || name.Split('/').Any(segment => segment is ".." or "."))
                {
                    problems.Add($"{name}: not a path inside the theme.");
                    continue;
                }

                totalBytes += entry.Length;
                if (totalBytes > MaxBytes)
                {
                    problems.Add($"The files are more than {MaxBytes} bytes unpacked.");
                    break;
                }

                if (name == ManifestName)
                {
                    (hasDefaultLayout, defaultLayout) = ReadManifest(entry, problems);
                    continue;
                }

                if (files.Count >= MaxFileCount)
                {
                    problems.Add($"The package has more than {MaxFileCount} files.");
                    break;
                }

                var path = SiteThemePaths.Normalize(name);
                if (!SiteThemePaths.IsValid(path))
                {
                    problems.Add($"{name}: not a valid path for a theme file (letters, digits, '_' and '-', separated by '/').");
                    continue;
                }

                if (!SiteThemePaths.IsAllowed(path))
                {
                    problems.Add($"{name}: '.{SiteThemePaths.GetExtension(path)}' is not an extension a theme file may have; a template has none.");
                    continue;
                }

                if (!files.TryAdd(path, string.Empty))
                {
                    problems.Add($"{name}: the package holds this path twice.");
                    continue;
                }

                var body = ReadText(entry, name, problems);
                if (body == null)
                {
                    files.Remove(path);
                    continue;
                }

                if (body.Length > SiteThemeConsts.MaxBodyLength)
                {
                    problems.Add($"{name}: longer than the {SiteThemeConsts.MaxBodyLength} characters a theme file may hold.");
                    files.Remove(path);
                    continue;
                }

                files[path] = body;
            }
        }

        if (problems.Count > 0)
        {
            throw Invalid(problems);
        }

        return new SiteThemePackageContent(files, hasDefaultLayout, defaultLayout);
    }

    private static bool IsSkipped(string name)
    {
        return name.StartsWith("__MACOSX/", StringComparison.Ordinal)
               || (name != ManifestName && name.Split('/').Any(segment => segment.StartsWith('.') && segment is not (".." or ".")));
    }

    private static (bool HasDefaultLayout, string? DefaultLayout) ReadManifest(ZipArchiveEntry entry, List<string> problems)
    {
        var text = ReadText(entry, ManifestName, problems);
        if (text == null)
        {
            return (false, null);
        }

        try
        {
            if (JsonNode.Parse(text) is not JsonObject manifest)
            {
                problems.Add($"{ManifestName}: not a JSON object.");
                return (false, null);
            }

            if (!manifest.TryGetPropertyValue("defaultLayout", out var value))
            {
                return (false, null);
            }

            if (value is null)
            {
                return (true, null);
            }

            if (value is JsonValue layout && layout.TryGetValue<string>(out var path))
            {
                return (true, string.IsNullOrWhiteSpace(path) ? null : SiteThemePaths.Normalize(path));
            }

            problems.Add($"{ManifestName}: defaultLayout must be the path of a template, or null.");
            return (false, null);
        }
        catch (JsonException)
        {
            problems.Add($"{ManifestName}: not valid JSON.");
            return (false, null);
        }
    }

    /// <summary>The entry as UTF-8 text without a byte order mark; null, with a problem added, when it is not UTF-8.</summary>
    private static string? ReadText(ZipArchiveEntry entry, string name, List<string> problems)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        // Read no more than the entry says it holds, and one byte past it to tell a lying header.
        var limit = entry.Length + 1;
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, (int)Math.Min(chunk.Length, limit - buffer.Length))) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= limit)
            {
                problems.Add($"{name}: holds more than its size in the package says.");
                return null;
            }
        }

        try
        {
            var text = StrictUtf8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
        }
        catch (DecoderFallbackException)
        {
            problems.Add($"{name}: not UTF-8 text. A theme holds text files only; fonts and images go in the file library.");
            return null;
        }
    }

    private static void WriteEntry(ZipArchive archive, string path, string body)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = StrictUtf8.GetBytes(body);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static BusinessException Invalid(IEnumerable<string> problems)
    {
        return new BusinessException(SiteErrorCodes.SiteThemePackageInvalid)
            .WithData("Problems", string.Join(Environment.NewLine, problems));
    }
}

/// <summary>What a package holds: its files by path, and the default layout when its manifest names one (or null).</summary>
public sealed record SiteThemePackageContent(
    IReadOnlyDictionary<string, string> Files,
    bool HasDefaultLayout,
    string? DefaultLayout);

/// <summary>What an import did to the working copy, path by path.</summary>
public sealed class SiteThemeImport
{
    public List<string> Added { get; } = new();

    public List<string> Modified { get; } = new();

    public List<string> Deleted { get; } = new();

    public List<string> Unchanged { get; } = new();

    /// <summary>Whether the default layout was set from the package.</summary>
    public bool DefaultLayoutSet { get; set; }
}
