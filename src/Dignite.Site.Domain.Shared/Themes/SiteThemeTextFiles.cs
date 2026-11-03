using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Dignite.Site.Themes;

/// <summary>What <see cref="SiteThemeTextFiles.Read"/> found in a theme's text file.</summary>
/// <param name="CultureName">The language the file holds texts for, in its canonical spelling (<c>zh-Hans</c>); null when the file is invalid.</param>
/// <param name="Texts">Key to text; null when the file is invalid.</param>
/// <param name="Error">What is wrong with the file, or null when nothing is.</param>
public sealed record SiteThemeTextFileReadResult(
    string? CultureName,
    IReadOnlyDictionary<string, string>? Texts,
    string? Error)
{
    public bool IsValid => Error == null;
}

/// <summary>
/// The texts of a theme, for <c>t</c>: <c>locales/{culture}.json</c>, one file per language, in ABP's localization
/// format - <c>{"culture": "ja", "texts": {"Key": "text"}}</c>, the same as the files a host keeps next to its Razor
/// views, so a file can move between the two. Comments and trailing commas are allowed.
/// <para>
/// The folder is what makes a file one: every <c>.json</c> file directly in <see cref="SiteThemeConsts.TextFilesFolder"/>
/// is read as texts, and one that is not valid is an error of the check rather than a file silently ignored. The
/// language is the file's name and must be the <c>culture</c> the file says, so that a copied <c>en.json</c>
/// edited into Japanese but not renamed is caught, not left to replace the English texts.
/// </para>
/// </summary>
public static class SiteThemeTextFiles
{
    private const string Extension = ".json";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Whether the (normalized) <paramref name="path"/> is a text file: <c>locales/{name}.json</c>.</summary>
    public static bool IsTextFile(string path)
    {
        return GetCultureName(path) != null;
    }

    /// <summary>
    /// The language the file's name says - <c>ja</c> for <c>locales/ja.json</c> - as written, or null when the path
    /// is not a text file.
    /// </summary>
    public static string? GetCultureName(string path)
    {
        var prefix = SiteThemeConsts.TextFilesFolder + "/";
        if (path == null
            || !path.StartsWith(prefix, StringComparison.Ordinal)
            || !path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = path[prefix.Length..^Extension.Length];
        return name.Length == 0 || name.Contains('/') ? null : name;
    }

    /// <summary>
    /// Reads a text file. Never throws for what is in the file: a file that does not parse, has no <c>culture</c> or
    /// <c>texts</c>, has a text that is not a string, or names another language than its own name says, is reported in
    /// <see cref="SiteThemeTextFileReadResult.Error"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not a text file; ask <see cref="IsTextFile"/> first.</exception>
    public static SiteThemeTextFileReadResult Read(string path, string body)
    {
        var nameCulture = GetCultureName(path)
                          ?? throw new ArgumentException($"'{path}' is not a text file of a theme.", nameof(path));

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body ?? string.Empty, JsonOptions);
        }
        catch (JsonException exception)
        {
            return Invalid($"it is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid("its content is not a JSON object.");
            }

            if (!root.TryGetProperty("culture", out var culture)
                || culture.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(culture.GetString()))
            {
                return Invalid("it has no \"culture\", the language of its texts.");
            }

            if (!root.TryGetProperty("texts", out var texts) || texts.ValueKind != JsonValueKind.Object)
            {
                return Invalid("it has no \"texts\" object.");
            }

            var declared = culture.GetString()!.Trim();
            var cultureName = Canonicalize(declared);
            if (!string.Equals(cultureName, Canonicalize(nameCulture), StringComparison.OrdinalIgnoreCase))
            {
                return Invalid($"its \"culture\" is \"{declared}\", but its name says \"{nameCulture}\".");
            }

            var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in texts.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return Invalid($"the text \"{property.Name}\" is not a string.");
                }

                dictionary[property.Name] = property.Value.GetString()!;
            }

            return new SiteThemeTextFileReadResult(cultureName, dictionary, null);
        }
    }

    /// <summary>
    /// A language name in the spelling lookups ask for (<c>zh-hans</c> becomes <c>zh-Hans</c>); a name the runtime does
    /// not know is kept as written.
    /// </summary>
    public static string Canonicalize(string cultureName)
    {
        try
        {
            var name = CultureInfo.GetCultureInfo(cultureName).Name;
            return name.Length == 0 ? cultureName : name;
        }
        catch (CultureNotFoundException)
        {
            return cultureName;
        }
    }

    private static SiteThemeTextFileReadResult Invalid(string error)
    {
        return new SiteThemeTextFileReadResult(null, null, error);
    }
}
