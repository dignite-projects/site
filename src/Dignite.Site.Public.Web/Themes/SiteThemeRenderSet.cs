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

    public SiteThemeRenderSet(SiteThemeSnapshotDto snapshot)
    {
        Snapshot = Check.NotNull(snapshot, nameof(snapshot));
        _filesByPath = snapshot.Files
            .GroupBy(file => file.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _texts = new Lazy<SiteThemeTextCatalog>(() => SiteThemeTextCatalog.Create(snapshot.Files));
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
}
