using System;
using System.Linq;
using Dignite.Abp.BlobManagement;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.Site.Public.Files;

/// <summary>
/// Where Site served its files before 3.0 - <c>GET|HEAD /api/site-public/files/{containerName}/{blobName}</c>, optionally
/// <c>?Width=&amp;Height=</c>, and <c>.../download/{containerName}/{blobName}</c> - kept so the addresses stored with
/// contents, settings and templates keep working. Each answers <c>301</c> with Blob Management's public address of the
/// same file (<see cref="BlobManagementPublicUrl"/>): Blob Management keeps the file under the physical BLOB name Site's
/// address carries, in the request's tenant, resolved like any other request (the host name, a header, ...). Nothing is
/// read here: Blob Management answers for the file, 404 included.
/// <para>
/// A size is passed on, rounded up as <see cref="SiteFileUrl.Sized"/> rounds one given alone; <c>Format</c> is dropped,
/// since Blob Management serves a file as stored. A download is redirected to the same address: Blob Management decides
/// inline or attachment by the file's type, and the name Site gave the download is not kept.
/// </para>
/// </summary>
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route(SiteFileUrl.RoutePrefix)]
public class FilePublicController : SitePublicController
{
    [HttpGet]
    [HttpHead]
    [Route("{containerName}/{*blobName}")]
    public virtual IActionResult Get(string containerName, string blobName, int? width = null, int? height = null)
    {
        return RedirectToBlobManagement(containerName, blobName, width, height);
    }

    [HttpGet]
    [HttpHead]
    [Route("download/{containerName}/{*blobName}")]
    public virtual IActionResult Download(string containerName, string blobName)
    {
        return RedirectToBlobManagement(containerName, blobName, null, null);
    }

    protected virtual IActionResult RedirectToBlobManagement(string containerName, string blobName, int? width, int? height)
    {
        if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(blobName))
        {
            return NotFound();
        }

        var address = BlobManagementPublicUrl.Build(containerName, blobName, CurrentTenant.Id);
        return RedirectPermanent(SiteFileUrl.Sized(address, width, height)!);
    }
}
