using System;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// The live release of the tenant's theme - what the site renders. Only the live release is ever returned: the
/// working copy and the other releases are not reachable through the public surface.
/// </summary>
public interface ISiteThemePublicAppService : IApplicationService
{
    /// <summary>
    /// The live release. Pass the id of the release the caller already holds as <paramref name="knownReleaseId"/>:
    /// when it is still the live one the answer carries no files (<see cref="SiteLiveThemeDto.IsUnchanged"/>), so a
    /// renderer that keeps the parsed release asks one cheap question per page view. Static files come without their
    /// bodies - a page only needs their hashes.
    /// </summary>
    Task<SiteLiveThemeDto> GetLiveAsync(Guid? knownReleaseId = null);

    /// <summary>
    /// A static file of the live release with its body, or null when the live release has no static file at that path
    /// (or nothing is live). What <c>GET /site-assets/{path}</c> serves.
    /// </summary>
    Task<SiteThemeAssetDto?> FindLiveAssetAsync(string path);
}

public class SiteLiveThemeDto
{
    /// <summary>The live release's id, or null when nothing was ever published.</summary>
    public Guid? ReleaseId { get; set; }

    /// <summary>Whether <see cref="ReleaseId"/> is the known release the caller passed - then <see cref="Snapshot"/> is null.</summary>
    public bool IsUnchanged { get; set; }

    /// <summary>The live release, when something is live and the caller does not hold it already.</summary>
    public SiteThemeSnapshotDto? Snapshot { get; set; }
}

public class SiteThemeAssetDto
{
    public string Path { get; set; } = default!;

    /// <summary>What <c>asset_url</c> puts in the address, and the response's ETag.</summary>
    public string ContentHash { get; set; } = default!;

    public string Body { get; set; } = default!;
}
