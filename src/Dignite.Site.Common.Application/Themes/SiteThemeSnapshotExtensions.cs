using System.Collections.Generic;
using System.Linq;

namespace Dignite.Site.Themes;

/// <summary>The one way a working copy or a release becomes the snapshot a renderer reads.</summary>
public static class SiteThemeSnapshotExtensions
{
    public static SiteThemeSnapshotDto ToSnapshotDto(this SiteTheme theme, IEnumerable<SiteThemeFile> files)
    {
        return new SiteThemeSnapshotDto
        {
            DefaultLayout = theme.DefaultLayout,
            Files = files
                .Select(file => new SiteThemeSnapshotFileDto { Path = file.Path, ContentHash = file.ContentHash, Body = file.Body })
                .ToList()
        };
    }

    /// <param name="contents">
    /// The release's files; an asset read without its body (<c>includeAssetBodies: false</c>) has an empty one.
    /// </param>
    public static SiteThemeSnapshotDto ToSnapshotDto(this SiteRelease release, IEnumerable<SiteReleaseFileContent> contents)
    {
        return new SiteThemeSnapshotDto
        {
            ReleaseId = release.Id,
            ReleaseNumber = release.Number,
            DefaultLayout = release.DefaultLayout,
            Files = contents
                .Select(content => new SiteThemeSnapshotFileDto
                {
                    Path = content.Path,
                    ContentHash = content.ContentHash,
                    Body = content.Body ?? string.Empty
                })
                .ToList()
        };
    }
}
