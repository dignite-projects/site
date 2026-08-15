using Volo.Abp.Identity;

namespace Dignite.Site.Host;

public static class SiteHostConsts
{
    public const string AdminEmailDefaultValue = IdentityDataSeedContributor.AdminEmailDefaultValue;
    public const string AdminPasswordDefaultValue = "1q2w3E*";

    /// <summary>
    /// Matches the audience passed to <c>options.AddAudiences("Host")</c> in SiteHostModule,
    /// and the "Host_App" client's "Host" scope in angular/src/environments/environment*.ts.
    /// </summary>
    public const string ApiScopeName = "Host";

    /// <summary>
    /// Matches clientId in angular/src/environments/environment*.ts.
    /// </summary>
    public const string AngularClientId = "Host_App";

    /// <summary>
    /// The pre-registered client an interactive MCP client (a desktop AI assistant) authenticates as.
    /// <para>
    /// Pre-registration is the MCP specification's own first-priority mechanism - its ordering is
    /// pre-registered credentials, then Client ID Metadata Documents, then Dynamic Client Registration,
    /// then prompting the user. DCR is deprecated as of the <c>2026-07-28</c> revision and is to be
    /// removed, so this is not a stopgap for it; a client that cannot discover the id asks the user for
    /// it, which is what this constant is published for.
    /// </para>
    /// </summary>
    public const string McpClientId = "Site_Mcp";

    /// <summary>
    /// The client an unattended job (publishing from CI) authenticates as, via
    /// <c>client_credentials</c>. Same authorization server and same permission checks as everything
    /// else - no separate API key scheme (总体设计 §6.2.5).
    /// </summary>
    public const string McpServiceClientId = "Site_Mcp_Service";
}
