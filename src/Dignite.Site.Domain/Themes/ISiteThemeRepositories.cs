using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Site.Themes;

public interface ISiteThemeRepository : IBasicRepository<SiteTheme, Guid>
{
    /// <summary>The current tenant's theme, or null when it has none yet.</summary>
    Task<SiteTheme?> FindCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The current tenant's live release id, or null when it has no theme or never published - one column, on
    /// every page view.
    /// </summary>
    Task<Guid?> FindLiveReleaseIdAsync(CancellationToken cancellationToken = default);
}

public interface ISiteThemeFileRepository : IBasicRepository<SiteThemeFile, Guid>
{
    Task<SiteThemeFile?> FindByPathAsync(Guid themeId, string path, CancellationToken cancellationToken = default);

    Task<bool> PathExistsAsync(Guid themeId, string path, CancellationToken cancellationToken = default);

    /// <summary>Every file of the working copy with its body, ordered by path.</summary>
    Task<List<SiteThemeFile>> GetListAsync(Guid themeId, CancellationToken cancellationToken = default);

    /// <summary>Every file of the working copy without its body, ordered by path - what a file list shows.</summary>
    Task<List<SiteThemeFileSummary>> GetSummariesAsync(Guid themeId, CancellationToken cancellationToken = default);
}

public interface ISiteReleaseRepository : IBasicRepository<SiteRelease, Guid>
{
    /// <summary>The releases of <paramref name="themeId"/>, newest first, without their files.</summary>
    Task<List<SiteRelease>> GetListAsync(Guid themeId, CancellationToken cancellationToken = default);

    /// <summary>The highest release number of <paramref name="themeId"/>, or 0 when it has none.</summary>
    Task<int> GetMaxNumberAsync(Guid themeId, CancellationToken cancellationToken = default);

    /// <summary>The files of <paramref name="releaseId"/>, path and content hash only, ordered by path.</summary>
    Task<List<SiteReleaseFile>> GetManifestAsync(Guid releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The files of <paramref name="releaseId"/> with their bodies, in one query, ordered by path. A static file's
    /// body is left null unless <paramref name="includeAssetBodies"/>: a page render only needs its hash.
    /// </summary>
    Task<List<SiteReleaseFileContent>> GetContentsAsync(
        Guid releaseId,
        bool includeAssetBodies = false,
        CancellationToken cancellationToken = default);

    /// <summary>One file of <paramref name="releaseId"/> with its body, or null when the release has no such path.</summary>
    Task<SiteReleaseFileContent?> FindContentAsync(Guid releaseId, string path, CancellationToken cancellationToken = default);
}

public interface ISiteThemeBlobRepository : IBasicRepository<SiteThemeBlob, Guid>
{
    /// <summary>Which of <paramref name="contentHashes"/> the current tenant already has a blob for.</summary>
    Task<HashSet<string>> GetExistingHashesAsync(IEnumerable<string> contentHashes, CancellationToken cancellationToken = default);
}

/// <summary>A working copy file without its body - see <see cref="ISiteThemeFileRepository.GetSummariesAsync"/>.</summary>
public sealed record SiteThemeFileSummary(
    Guid Id,
    string Path,
    string ContentHash,
    DateTime CreationTime,
    DateTime? LastModificationTime);

/// <summary>A release file with its body - see <see cref="ISiteReleaseRepository.GetContentsAsync"/>.</summary>
public sealed record SiteReleaseFileContent(string Path, string ContentHash, string? Body);
