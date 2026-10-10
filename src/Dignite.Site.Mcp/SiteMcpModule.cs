using Dignite.Abp.AspNetCore.Mcp;
using Dignite.Site.Mcp.ContentTypes;
using Dignite.Site.Mcp.Contents;
using Dignite.Site.Files;
using Dignite.Site.Mcp.Fields;
using Dignite.Site.Mcp.Files;
using Dignite.Site.Mcp.Pages;
using Dignite.Site.Mcp.Routing;
using Dignite.Site.Mcp.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Modularity;

namespace Dignite.Site.Mcp;

/// <summary>
/// Exposes the site's authoring capability over MCP (总体设计 §6.1, §6.2; GitHub issue #26), as the
/// <c>site</c> namespace of the application's MCP server.
/// <para>
/// <b>The tools are another caller, not a second implementation.</b> Every write goes through the same
/// Admin application services the HTTP API uses, which go through the same domain managers - so slug
/// uniqueness, culture normalization, page/type consistency and field validation are enforced once,
/// where they already were (§6.2.2).
/// </para>
/// <para>
/// <b>This module contributes tools; it does not host the server.</b> Transport, the <c>/mcp</c>
/// endpoint, <c>tools/list</c> permission filtering, the structured error envelope and server info belong
/// to <see cref="AbpAspNetCoreMcpModule"/>, because the SDK keeps exactly one server per application and
/// any other module's tools share it. Configuring any of
/// those here would be configuring them for every module on the server. A deployment configures them in
/// its host (总体设计 §6.2.7).
/// </para>
/// </summary>
[DependsOn(
    // The unified contracts: Admin app services (MCP is an authoring surface, so it must see drafts)
    // plus the Public routing service behind site_resolve_path.
    typeof(SiteApplicationContractsModule),
    typeof(AbpAspNetCoreMcpModule)
)]
public class SiteMcpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Every tool name starts with "site_" and every resource uses the site:// scheme - the server
        // refuses to start otherwise. That is what keeps names as generic as list_fields from colliding
        // with another module's on the same server, where the SDK would silently keep only one of them.
        context.Services.AddAbpMcpModule(SiteMcpConsts.ModuleName, mcp => mcp
            .AddTools<SiteSchemaTools>()
            .AddTools<ContentTools>()
            .AddTools<PageTools>()
            .AddTools<ContentTypeTools>()
            .AddTools<FieldTools>()
            .AddTools<RoutingTools>()
            .AddTools<FileContainerTools>()
            .AddTools<DirectoryTools>()
            .AddTools<FileTools>()
            .AddResources<SiteSchemaResources>()
            .AddInstructions(SiteMcpConsts.Instructions)
            .AddInstructions(SiteMcpConsts.FileInstructions));

        // The file tools reach both of Site's containers unless the host narrows the list. Same containers,
        // same per-container permissions (SiteAdminApplicationModule) as an upload from the admin UI.
        context.Services.Configure<SiteMcpFileOptions>(options =>
        {
            options.Containers
                .Add(SiteFileContainerNames.Images,
                    "Pictures for site content (raster images only, no SVG). Use this for any image a content field will show.")
                .Add(SiteFileContainerNames.Default,
                    "General attachments for site content - documents and images that are downloaded rather than shown.");
        });

        // Upload bytes travel base64-encoded in the request body, so the endpoint's body limit has to make
        // room for SiteMcpFileOptions.MaxUploadSize - while still refusing anything larger before it is read.
        context.Services.AddTransient<IPostConfigureOptions<AbpMcpServerOptions>, SiteMcpFileServerOptionsSetup>();
    }
}
