using Dignite.Site.Mcp.ContentTypes;
using Dignite.Site.Mcp.Contents;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Mcp.Fields;
using Dignite.Site.Mcp.Pages;
using Dignite.Site.Mcp.Routing;
using Dignite.Site.Mcp.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using Volo.Abp.AspNetCore;
using Volo.Abp.Modularity;

namespace Dignite.Site.Mcp;

/// <summary>
/// Exposes the site's authoring capability as an MCP server (总体设计 §6.1, §6.2; GitHub issue #26).
/// <para>
/// <b>The tools are another caller, not a second implementation.</b> Every write goes through the same
/// Admin application services the HTTP API uses, which go through the same domain managers - so slug
/// uniqueness, culture normalization, page/type consistency and field validation are enforced once,
/// where they already were (§6.2.2).
/// </para>
/// <para>
/// <b>Authentication, multi-tenancy and the unit of work cost nothing here</b>, and that is a
/// consequence of the transport rather than luck. The Streamable HTTP transport runs stateless by
/// default (the <c>2026-07-28</c> protocol revision), so every MCP request is an ordinary HTTP request
/// through the host's ordinary pipeline: OpenIddict has validated the bearer token, ABP has resolved the
/// tenant from its <c>tenantid</c> claim and opened a unit of work, all before the endpoint runs. Hence
/// the same tokens as the HTTP API, and no MCP-specific permission scheme (§6.2.5).
/// </para>
/// </summary>
[DependsOn(
    // The unified contracts: Admin app services (MCP is an authoring surface, so it must see drafts)
    // plus the Public routing service behind resolve_path.
    typeof(SiteApplicationContractsModule),
    // For AbpEndpointRouterOptions - the seam that lets this module map an endpoint without owning any
    // middleware ordering of its own.
    typeof(AbpAspNetCoreModule)
)]
public class SiteMcpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var options = context.Services.ExecutePreConfiguredActions<SiteMcpOptions>();

        Configure<SiteMcpOptions>(mcpOptions =>
        {
            mcpOptions.RoutePattern = options.RoutePattern;
            mcpOptions.ServerName = options.ServerName;
            mcpOptions.ServerVersion = options.ServerVersion;
            mcpOptions.Instructions = options.Instructions;
            mcpOptions.AuthenticationSchemes.AddRange(options.AuthenticationSchemes);
        });

        context.Services
            .AddMcpServer(serverOptions =>
            {
                serverOptions.ServerInfo = new Implementation
                {
                    Name = options.ServerName,
                    Version = options.ServerVersion
                };

                serverOptions.ServerInstructions = options.Instructions;
            })
            .WithHttpTransport()
            // Honours [Authorize] on tools and resources in two places: tools the current user cannot
            // call never appear in tools/list, and a call that slips through is refused. The first is
            // not only a safety property - it saves the model a turn it would have spent on a guaranteed
            // 403 (总体设计 §6.2.5).
            .AddAuthorizationFilters()
            .WithTools<SiteSchemaTools>()
            .WithTools<ContentTools>()
            .WithTools<PageTools>()
            .WithTools<ContentTypeTools>()
            .WithTools<FieldTools>()
            .WithTools<RoutingTools>()
            .WithResources<SiteSchemaResources>()
            // One place turns a domain exception into a machine-readable failure, rather than eighteen
            // try/catch blocks that would drift apart (总体设计 §6.2.4).
            .WithRequestFilters(filters => filters.AddCallToolFilter(McpErrorResultFactory.CallToolFilter));

        Configure<AbpEndpointRouterOptions>(routerOptions =>
        {
            routerOptions.EndpointConfigureActions.Add(endpointContext =>
            {
                var mcpOptions = endpointContext.ScopeServiceProvider
                    .GetRequiredService<IOptions<SiteMcpOptions>>().Value;

                // RequireAuthorization, not an anonymous endpoint: 总体设计 §6.2.5 rules out an anonymous
                // MCP surface for now - the Public app services serve published content only and opening
                // *those* anonymously is a separate decision, entangled with the §5.6 AI-crawler toggles.
                //
                // The explicit scheme list matters as much as the requirement itself - without it an ABP
                // MVC host answers an unauthenticated MCP request with a 302 to its login page instead of
                // the 401 an MCP client needs. See SiteMcpOptions.AuthenticationSchemes.
                var endpoint = endpointContext.Endpoints.MapMcp(mcpOptions.RoutePattern);

                if (mcpOptions.AuthenticationSchemes.Count > 0)
                {
                    endpoint.RequireAuthorization(new AuthorizeAttribute
                    {
                        AuthenticationSchemes = string.Join(",", mcpOptions.AuthenticationSchemes)
                    });
                }
                else
                {
                    endpoint.RequireAuthorization();
                }
            });
        });
    }
}
