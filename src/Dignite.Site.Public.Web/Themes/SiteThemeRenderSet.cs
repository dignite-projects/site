using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Public.Localization;
using Dignite.Site.Themes;
using Volo.Abp;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// A theme snapshot (<see cref="SiteThemeSnapshotDto"/>) as one page render reads it: its files looked up by path.
/// The live release's is built once per release and shared by every request until the next publish
/// (<see cref="SiteLiveThemeProvider"/>); a preview builds one from the working copy or a release.
/// </summary>
public class SiteThemeRenderSet
{
    private readonly Dictionary<string, SiteThemeSnapshotFileDto> _filesByPath;
    private readonly Lazy<SiteThemeTextCatalog> _texts;
    private readonly HashSet<string> _templatesWithLanguageVersions;

    public SiteThemeRenderSet(SiteThemeSnapshotDto snapshot)
    {
        Snapshot = Check.NotNull(snapshot, nameof(snapshot));
        _filesByPath = snapshot.Files
            .GroupBy(file => file.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _texts = new Lazy<SiteThemeTextCatalog>(() => SiteThemeTextCatalog.Create(snapshot.Files));

        // Which templates have a version for some language, so that a theme with none - the usual one - looks a
        // template up once, exactly as before, and one with some pays only for the templates that have them.
        _templatesWithLanguageVersions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in _filesByPath.Keys)
        {
            if (SiteThemePaths.TryGetLanguageVersion(path, out var templatePath, out _))
            {
                _templatesWithLanguageVersions.Add(templatePath);
            }
        }
    }

    public SiteThemeSnapshotDto Snapshot { get; }

    /// <summary>
    /// The theme's own texts, its <c>locales/*.json</c> files, parsed on first use and then shared by every request
    /// that renders this set.
    /// </summary>
    public SiteThemeTextCatalog Texts => _texts.Value;

    /// <summary>The release it is, or null for a working copy.</summary>
    public Guid? ReleaseId => Snapshot.ReleaseId;

    /// <summary>The layout of a page template whose front matter names none.</summary>
    public string? DefaultLayout => Snapshot.DefaultLayout;

    /// <summary>The total length of the bodies it holds - what a cache weighs it by.</summary>
    public long Size => Snapshot.Files.Sum(file => (long)(file.Body?.Length ?? 0) + file.Path.Length);

    /// <summary>The file at the (normalized) <paramref name="path"/>, template or static file, or null.</summary>
    public SiteThemeSnapshotFileDto? FindFile(string path)
    {
        return _filesByPath.GetValueOrDefault(path);
    }

    /// <summary>The Scriban template at the (normalized) <paramref name="path"/>, or null when there is none - or it is a static file.</summary>
    public SiteThemeSnapshotFileDto? FindTemplate(string path)
    {
        return SiteThemePaths.IsTemplate(path) ? FindFile(path) : null;
    }

    /// <summary>
    /// The template to render for <paramref name="path"/> in the language the page is in: the first of its language
    /// versions to exist, going through <paramref name="cultureChain"/> from the most specific language
    /// (<c>Default.zh-Hant-TW</c>, <c>Default.zh-Hant</c>, <c>Default.zh</c>), else the template itself. Null when
    /// there is no template at <paramref name="path"/>: a language version alone is not one, so that a page does not
    /// render by Scriban in one language and by its Razor view in the others.
    /// </summary>
    public SiteThemeSnapshotFileDto? FindTemplate(string path, IReadOnlyList<string>? cultureChain)
    {
        var template = FindTemplate(path);
        if (template == null || cultureChain == null || !_templatesWithLanguageVersions.Contains(path))
        {
            return template;
        }

        foreach (var cultureName in cultureChain)
        {
            if (_filesByPath.TryGetValue(SiteThemePaths.GetLanguageVersionPath(path, cultureName), out var version))
            {
                return version;
            }
        }

        return template;
    }
}
