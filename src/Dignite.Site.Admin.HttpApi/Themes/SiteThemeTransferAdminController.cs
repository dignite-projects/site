using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// The theme as a zip, out and back in, over HTTP - and the one-time links of the <c>site_create_theme_download_url</c>
/// and <c>site_create_theme_upload_url</c> MCP tools, which a command line follows without a credential of its own.
/// </summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Themes.Default)]
[Route("api/site-admin/theme")]
public class SiteThemeTransferAdminController : SiteAdminController, ISiteThemeTransferAdminAppService
{
    protected ISiteThemeTransferAdminAppService TransferAppService { get; }
    protected ISiteThemeTransferTicketService TicketService { get; }

    public SiteThemeTransferAdminController(
        ISiteThemeTransferAdminAppService transferAppService,
        ISiteThemeTransferTicketService ticketService)
    {
        TransferAppService = transferAppService;
        TicketService = ticketService;
    }

    [HttpGet]
    [Route("export")]
    public virtual Task<IRemoteStreamContent> ExportAsync(Guid? releaseId = null)
    {
        return TransferAppService.ExportAsync(releaseId);
    }

    [HttpPost]
    [Route("import")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task<SiteThemeImportResultDto> ImportAsync([FromForm] ImportSiteThemeInput input)
    {
        return TransferAppService.ImportAsync(input);
    }

    [HttpPost]
    [Route("export-links")]
    public virtual Task<SiteThemeTransferLinkDto> CreateExportLinkAsync(CreateSiteThemeExportLinkInput input)
    {
        return TransferAppService.CreateExportLinkAsync(input);
    }

    [HttpPost]
    [Route("import-links")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task<SiteThemeTransferLinkDto> CreateImportLinkAsync(CreateSiteThemeImportLinkInput input)
    {
        return TransferAppService.CreateImportLinkAsync(input);
    }

    /// <summary>The zip an export link names. Anonymous: the ticket is the credential - signed, short-lived, single use.</summary>
    [HttpGet]
    [Route(SiteThemeTransferPaths.DownloadRouteTemplate)]
    [AllowAnonymous]
    public virtual Task<IRemoteStreamContent> DownloadByTicketAsync(string ticket)
    {
        return TicketService.DownloadAsync(ticket);
    }

    /// <summary>
    /// Imports the package sent as the raw body (<c>PUT</c>, any content type) the way an import link says. Anonymous: the
    /// ticket is the credential. The body is bounded by <see cref="SiteThemePackage.MaxBytes"/> before it is read.
    /// </summary>
    [HttpPut]
    [Route(SiteThemeTransferPaths.UploadRouteTemplate)]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [DisableFormValueModelBinding]
    public virtual Task<SiteThemeImportResultDto> UploadByTicketAsync(string ticket)
    {
        return TicketService.UploadAsync(ticket, maxBytes =>
        {
            var feature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = maxBytes;
            }

            return Request.Body;
        });
    }
}
