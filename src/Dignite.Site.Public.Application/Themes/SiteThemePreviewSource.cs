using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// Reads the working copy, or a release, for the site's preview - see <see cref="ISiteThemePreviewSource"/> for why this
/// is not part of <see cref="ISiteThemePublicAppService"/>.
/// <para>
/// A plain class rather than a <see cref="SitePublicAppService"/>: an application service is exposed as an HTTP endpoint
/// by convention, and the working copy must never be. Every query is filtered by the current tenant, so a release of
/// another tenant is not found. The working copy is read whole on every page of a preview, which is fine: only the
/// theme's author, holding a preview cookie, ever causes this read.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteThemePreviewSource), typeof(SiteThemePreviewSource))]
public class SiteThemePreviewSource : ISiteThemePreviewSource, ITransientDependency, IUnitOfWorkEnabled
{
    protected ISiteThemeRepository ThemeRepository { get; }
    protected ISiteThemeFileRepository FileRepository { get; }
    protected ISiteReleaseRepository ReleaseRepository { get; }

    public SiteThemePreviewSource(
        ISiteThemeRepository themeRepository,
        ISiteThemeFileRepository fileRepository,
        ISiteReleaseRepository releaseRepository)
    {
        ThemeRepository = themeRepository;
        FileRepository = fileRepository;
        ReleaseRepository = releaseRepository;
    }

    public virtual async Task<SiteThemeSnapshotDto?> FindSnapshotAsync(Guid? releaseId, CancellationToken cancellationToken = default)
    {
        var theme = await ThemeRepository.FindCurrentAsync(cancellationToken);
        if (theme == null)
        {
            return null;
        }

        if (releaseId == null)
        {
            var files = await FileRepository.GetListAsync(theme.Id, cancellationToken);
            return new SiteThemeSnapshotDto
            {
                DefaultLayout = theme.DefaultLayout,
                Files = files
                    .Select(file => new SiteThemeSnapshotFileDto { Path = file.Path, ContentHash = file.ContentHash, Body = file.Body })
                    .ToList()
            };
        }

        var release = await FindReleaseAsync(theme, releaseId.Value, cancellationToken);
        if (release == null)
        {
            return null;
        }

        var contents = await ReleaseRepository.GetContentsAsync(release.Id, cancellationToken: cancellationToken);
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

    public virtual async Task<SiteThemeAssetDto?> FindAssetAsync(Guid? releaseId, string path, CancellationToken cancellationToken = default)
    {
        var normalized = SiteThemePaths.Normalize(path ?? string.Empty);
        if (!SiteThemePaths.IsValid(normalized) || !SiteThemePaths.IsAsset(normalized))
        {
            return null;
        }

        var theme = await ThemeRepository.FindCurrentAsync(cancellationToken);
        if (theme == null)
        {
            return null;
        }

        if (releaseId == null)
        {
            var file = await FileRepository.FindByPathAsync(theme.Id, normalized, cancellationToken);
            return file == null
                ? null
                : new SiteThemeAssetDto { Path = file.Path, ContentHash = file.ContentHash, Body = file.Body };
        }

        var release = await FindReleaseAsync(theme, releaseId.Value, cancellationToken);
        if (release == null)
        {
            return null;
        }

        var content = await ReleaseRepository.FindContentAsync(release.Id, normalized, cancellationToken);
        return content == null
            ? null
            : new SiteThemeAssetDto { Path = content.Path, ContentHash = content.ContentHash, Body = content.Body ?? string.Empty };
    }

    /// <summary>The release, when it is one of <paramref name="theme"/>'s; the repository already filters by tenant.</summary>
    protected virtual async Task<SiteRelease?> FindReleaseAsync(SiteTheme theme, Guid releaseId, CancellationToken cancellationToken)
    {
        var release = await ReleaseRepository.FindAsync(releaseId, includeDetails: false, cancellationToken);
        return release != null && release.ThemeId == theme.Id ? release : null;
    }
}
