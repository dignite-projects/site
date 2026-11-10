using System;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// The tenant's theme: a working copy of files that saving changes in place, published as a whole into immutable
/// releases. A file without an extension is a Scriban template, one with an extension a static file; no directory is
/// reserved. Nothing here changes what the site renders except <see cref="PublishAsync"/> and
/// <see cref="SetLiveReleaseAsync"/>.
/// <para>
/// The theme is created on first use, with a default layout (<c>layouts/default</c>) and an empty stylesheet
/// (<c>css/site.css</c>); creating it publishes nothing.
/// </para>
/// </summary>
public interface ISiteThemeAdminAppService : IApplicationService
{
    Task<SiteThemeDto> GetAsync();

    /// <summary>Changes the working copy's default layout. Covered by the Edit permission.</summary>
    Task<SiteThemeDto> UpdateAsync(UpdateSiteThemeDto input);

    /// <summary>Every file of the working copy, and every file the live release has that the working copy no longer does, by path.</summary>
    Task<ListResultDto<SiteThemeFileSummaryDto>> GetFilesAsync();

    /// <summary>One file with its body and its syntax errors. <c>EntityNotFoundException</c> when there is no such path.</summary>
    Task<SiteThemeFileDto> GetFileAsync(string path);

    /// <summary>
    /// Creates or overwrites a file. A template that does not parse is saved, with its errors in the result. Refused:
    /// a path of the wrong shape, an extension no static file may have, and a stale concurrency stamp.
    /// </summary>
    Task<SiteThemeFileDto> SaveFileAsync(SaveSiteThemeFileDto input);

    /// <summary>Moves a file, rewriting what names it when asked to - see <see cref="RenameSiteThemeFileDto.UpdateReferences"/>.</summary>
    Task<SiteThemeRenameResultDto> RenameFileAsync(RenameSiteThemeFileDto input);

    /// <summary>Removes a file from the working copy. The live site keeps it until the next publish.</summary>
    Task DeleteFileAsync(string path);

    /// <summary>The pages, files and setting that use a file, by literal name - what to look at before deleting it.</summary>
    Task<SiteThemeFileUsagesDto> GetFileUsagesAsync(string path);

    /// <summary>
    /// Checks the whole working copy, and lists what publishing it would change. Reading it covers every file; rendering
    /// covers the pages <paramref name="input"/> asks for - all of them when it is left out.
    /// </summary>
    Task<SiteThemeCheckDto> CheckAsync(SiteThemeCheckInput? input = null);

    /// <summary>
    /// Publishes the working copy as a new release and makes it live. Refused while the check finds an error
    /// (<c>Site:090002</c>), when the working copy changed since <paramref name="input"/>'s fingerprint was taken
    /// (<c>Site:090003</c>), and when nothing changed since the live release (<c>Site:090010</c>).
    /// </summary>
    Task<SiteReleaseDto> PublishAsync(PublishSiteThemeDto input);

    /// <summary>The releases, newest first.</summary>
    Task<ListResultDto<SiteReleaseDto>> GetReleasesAsync();

    /// <summary>One release with its file list.</summary>
    Task<SiteReleaseDetailDto> GetReleaseAsync(Guid id);

    /// <summary>One file of a release with its body, to compare with the working copy.</summary>
    Task<SiteReleaseFileContentDto> GetReleaseFileAsync(Guid id, string path);

    /// <summary>Makes a release the one the site renders - an older one is the rollback. Covered by the Publish permission.</summary>
    Task<SiteReleaseDto> SetLiveReleaseAsync(Guid id);

    /// <summary>
    /// Replaces the working copy with a release's files and default layout, deleting files the release does not have.
    /// Nothing goes live. Covered by the Edit permission.
    /// </summary>
    Task RestoreReleaseAsync(Guid id);

    /// <summary>
    /// Renders the site page at <paramref name="path"/> (<c>/about</c>, <c>/ja/blog/hello</c>) with the working copy, or
    /// with release <paramref name="releaseId"/>, in place of the live release, and returns its HTML. Nothing is
    /// published or cached for visitors.
    /// <para>
    /// Rendered by <c>Dignite.Site.Public.Web</c>: in the same application, or in the application that serves the site,
    /// named by <c>RemoteServices:SitePublicWeb</c>; with neither it fails with <c>Site:090007</c>. A path no page answers
    /// fails with <c>Site:090006</c>, a page whose template is not in the theme with <c>Site:090009</c>, a template that
    /// fails to render with <c>Site:090008</c>.
    /// </para>
    /// </summary>
    Task<SiteThemePreviewDto> PreviewAsync(string path, Guid? releaseId = null);

    /// <summary>
    /// A link that opens the site at <paramref name="path"/> (the home page when null) in a browser with the working copy
    /// as saved, or with release <paramref name="releaseId"/>, in place of the live release - for whoever opens it, within
    /// minutes, and then for a couple of hours: every page renders with that theme under a banner, until the banner's
    /// exit. Visitors keep seeing the live release.
    /// <para>
    /// The link goes to the tenant's site address, the <c>Site.PrimaryDomain</c> setting, and fails with
    /// <c>Site:050001</c> when there is none. The site reads its token with the application's Data Protection keys, so an
    /// administration API deployed apart from the site must share the site's key ring.
    /// </para>
    /// </summary>
    Task<SiteThemePreviewLinkDto> GetPreviewLinkAsync(string? path = null, Guid? releaseId = null);
}
