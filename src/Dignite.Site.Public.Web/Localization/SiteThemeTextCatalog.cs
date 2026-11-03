using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Themes;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The texts a Scriban theme carries: its <c>locales/{culture}.json</c> files (<see cref="SiteThemeTextFiles"/>),
/// parsed once per theme snapshot and shared by every request that renders it. <c>t</c> reads them and nothing else:
/// the host's own files (<see cref="SiteTemplateLocalizationContributor"/>) are for Razor views.
/// <para>
/// A file that is not valid is skipped without a word: a theme is published only after its check, which reports it
/// as an error, so one in a live release is not expected, and a render is not the place to log it on every request.
/// Of two files for the same language the first path wins, for the same reason.
/// </para>
/// </summary>
public class SiteThemeTextCatalog
{
    public static SiteThemeTextCatalog Empty { get; } = new(new Dictionary<string, IReadOnlyDictionary<string, string>>());

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _textsByCulture;

    private SiteThemeTextCatalog(Dictionary<string, IReadOnlyDictionary<string, string>> textsByCulture)
    {
        _textsByCulture = textsByCulture;
    }

    public static SiteThemeTextCatalog Create(IEnumerable<SiteThemeSnapshotFileDto> files)
    {
        var textsByCulture = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files
                     .Where(file => SiteThemeTextFiles.IsTextFile(file.Path))
                     .OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            var result = SiteThemeTextFiles.Read(file.Path, file.Body);
            if (result.IsValid)
            {
                textsByCulture.TryAdd(result.CultureName!, result.Texts!);
            }
        }

        return textsByCulture.Count == 0 ? Empty : new SiteThemeTextCatalog(textsByCulture);
    }

    /// <summary>The text of <paramref name="key"/> in exactly <paramref name="cultureName"/>, or null.</summary>
    public virtual string? GetOrNull(string cultureName, string key)
    {
        return _textsByCulture.TryGetValue(cultureName, out var texts) && texts.TryGetValue(key, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// The text of <paramref name="key"/> in the first of <paramref name="cultureChain"/> that has it
    /// (<see cref="SiteTemplateCultureChain"/>), or null.
    /// </summary>
    public virtual string? Find(IEnumerable<string> cultureChain, string key)
    {
        foreach (var cultureName in cultureChain)
        {
            var text = GetOrNull(cultureName, key);
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }
}
