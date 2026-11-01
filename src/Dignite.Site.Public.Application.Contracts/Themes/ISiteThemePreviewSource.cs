using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// The working copy, or any release, of the current tenant's theme - for the site's preview: a visitor holding a valid
/// preview ticket sees the site rendered with the theme the ticket names.
/// <para>
/// An in-process hook, not an application service, on purpose: <see cref="ISiteThemePublicAppService"/> only ever
/// answers with the live release, and that is what keeps the working copy out of reach of an anonymous visitor. This
/// interface gets no controller and no client proxy, so the working copy is never reachable over HTTP; only the
/// rendering side asks for it, and only after it has verified a ticket. A host that renders the site against a remote
/// Public API has no implementation, and the preview is then simply off.
/// </para>
/// </summary>
public interface ISiteThemePreviewSource
{
    /// <summary>
    /// The working copy as it is saved (<paramref name="releaseId"/> null), or the release <paramref name="releaseId"/>;
    /// null when the tenant has no theme or no such release. Like the live snapshot, static files may come without their
    /// bodies: a page only needs their hashes.
    /// </summary>
    Task<SiteThemeSnapshotDto?> FindSnapshotAsync(Guid? releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A static file of the working copy (<paramref name="releaseId"/> null) or of the release <paramref name="releaseId"/>,
    /// with its body; null when there is no static file at that path.
    /// </summary>
    Task<SiteThemeAssetDto?> FindAssetAsync(Guid? releaseId, string path, CancellationToken cancellationToken = default);
}
