using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Schema;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Schema;

/// <summary>
/// The one tool that has to be called before any writing can sensibly happen (总体设计 §6.2.4).
/// </summary>
[McpServerToolType]
public class SiteSchemaTools : ITransientDependency
{
    protected ISiteSchemaAdminAppService SiteSchemaAppService { get; }

    public SiteSchemaTools(ISiteSchemaAdminAppService siteSchemaAppService)
    {
        SiteSchemaAppService = siteSchemaAppService;
    }

    [McpServerTool(Name = "get_site_schema", Title = "Get the site's content schema", ReadOnly = true)]
    [Description(
        "Returns everything needed to write content for this site: the enabled languages (the first is " +
        "the default and its URLs carry no language prefix), the primary domain, every page with its " +
        "route, and under each page every content type with its fields. Fields carry the name to use as " +
        "the key when writing values, the field type and its configuration (length limits, a select " +
        "field's option list, and so on - what a value has to fit), whether this content type requires " +
        "them, and a description of what belongs in them. Call this first - every other tool is " +
        "addressed by these names, and they are this site's own data, not anything that can be guessed.")]
    [Authorize(AdminPermissions.Pages.Default)]
    [Authorize(AdminPermissions.ContentTypes.Default)]
    [Authorize(AdminPermissions.Fields.Default)]
    public virtual Task<SiteSchemaDto> GetSiteSchemaAsync()
    {
        return SiteSchemaAppService.GetAsync();
    }
}
