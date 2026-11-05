using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// Takes the theme out of the site as one zip and brings it back (dev-docs/site-theme-import-export.md): to build its
/// stylesheet where its templates can be read, to bring many files in at once, or to keep a copy elsewhere. An import
/// goes into the working copy only; it is checked and published as any change is.
/// </summary>
public interface ISiteThemeTransferAdminAppService : IApplicationService
{
    /// <summary>The working copy, or the release <paramref name="releaseId"/>, as a zip.</summary>
    Task<IRemoteStreamContent> ExportAsync(Guid? releaseId = null);

    /// <summary>Brings a package into the working copy, merged or replacing it.</summary>
    Task<SiteThemeImportResultDto> ImportAsync(ImportSiteThemeInput input);

    /// <summary>A link that downloads <see cref="ExportAsync"/>'s zip, for a client that saves it without its bytes passing through it.</summary>
    Task<SiteThemeTransferLinkDto> CreateExportLinkAsync(CreateSiteThemeExportLinkInput input);

    /// <summary>A link that takes a package as the body of a <c>PUT</c> and imports it.</summary>
    Task<SiteThemeTransferLinkDto> CreateImportLinkAsync(CreateSiteThemeImportLinkInput input);
}

/// <summary>Redeems the links of <see cref="ISiteThemeTransferAdminAppService"/>, for the anonymous endpoints that serve them.</summary>
public interface ISiteThemeTransferTicketService
{
    /// <exception cref="Volo.Abp.BusinessException"><c>Site:090015</c>: the link has expired, was used, or is not this site's.</exception>
    Task<IRemoteStreamContent> DownloadAsync(string ticket);

    /// <param name="ticket">The link's ticket.</param>
    /// <param name="openBody">Opens the request body, given the most bytes it may hold.</param>
    /// <exception cref="Volo.Abp.BusinessException"><c>Site:090015</c>: the link has expired, was used, or is not this site's.</exception>
    Task<SiteThemeImportResultDto> UploadAsync(string ticket, Func<long, Stream> openBody);
}
