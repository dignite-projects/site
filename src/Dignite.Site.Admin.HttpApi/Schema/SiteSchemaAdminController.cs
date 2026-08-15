using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Site.Admin.Schema;

/// <summary>
/// The site schema over HTTP. Primarily consumed as an MCP tool and resource (总体设计 §6.2.4), but it is
/// an ordinary read like any other, so it is exposed here too - a back-office or an external front end
/// building an editor form needs the same merged view.
/// </summary>
[RemoteService(Name = SiteAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SiteAdminRemoteServiceConsts.ModuleName)]
[Authorize(SiteAdminPermissions.Pages.Default)]
[Authorize(SiteAdminPermissions.ContentTypes.Default)]
[Authorize(SiteAdminPermissions.Fields.Default)]
[Route("api/site-admin/schema")]
public class SiteSchemaAdminController : SiteAdminController, ISiteSchemaAdminAppService
{
    protected ISiteSchemaAdminAppService SiteSchemaAdminAppService { get; }

    public SiteSchemaAdminController(ISiteSchemaAdminAppService siteSchemaAdminAppService)
    {
        SiteSchemaAdminAppService = siteSchemaAdminAppService;
    }

    [HttpGet]
    public virtual Task<SiteSchemaDto> GetAsync()
    {
        return SiteSchemaAdminAppService.GetAsync();
    }
}
