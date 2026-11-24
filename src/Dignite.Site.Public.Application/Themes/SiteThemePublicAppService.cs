using System;
using System.Threading.Tasks;
using Dignite.Site.Themes;

namespace Dignite.Site.Public.Themes;

public class SiteThemePublicAppService : SitePublicAppService, ISiteThemePublicAppService
{
    protected ISiteThemeRepository ThemeRepository { get; }
    protected ISiteReleaseRepository ReleaseRepository { get; }

    public SiteThemePublicAppService(ISiteThemeRepository themeRepository, ISiteReleaseRepository releaseRepository)
    {
        ThemeRepository = themeRepository;
        ReleaseRepository = releaseRepository;
    }

    public virtual async Task<SiteLiveThemeDto> GetLiveAsync(Guid? knownReleaseId = null)
    {
        var liveReleaseId = await ThemeRepository.FindLiveReleaseIdAsync();
        if (liveReleaseId == null)
        {
            return new SiteLiveThemeDto();
        }

        if (liveReleaseId == knownReleaseId)
        {
            return new SiteLiveThemeDto { ReleaseId = liveReleaseId, IsUnchanged = true };
        }

        var release = await ReleaseRepository.GetAsync(liveReleaseId.Value, includeDetails: false);
        var contents = await ReleaseRepository.GetContentsAsync(release.Id);

        return new SiteLiveThemeDto { ReleaseId = release.Id, Snapshot = release.ToSnapshotDto(contents) };
    }

    public virtual async Task<SiteThemeAssetDto?> FindLiveAssetAsync(string path)
    {
        if (SiteThemeAssets.NormalizePath(path) is not { } normalized)
        {
            return null;
        }

        var liveReleaseId = await ThemeRepository.FindLiveReleaseIdAsync();
        if (liveReleaseId == null)
        {
            return null;
        }

        var content = await ReleaseRepository.FindContentAsync(liveReleaseId.Value, normalized);

        return content?.ToAssetDto();
    }
}
