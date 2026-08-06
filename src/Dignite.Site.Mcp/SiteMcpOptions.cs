using System.Collections.Generic;

namespace Dignite.Site.Mcp;

/// <summary>
/// How the MCP endpoint is exposed. Everything a deploying host is likely to want to move.
/// <para>
/// <b>Always set these with <c>PreConfigure&lt;SiteMcpOptions&gt;</c>, never <c>Configure</c>.</b>
/// <see cref="SiteMcpModule"/> reads <see cref="ServerName"/>, <see cref="ServerVersion"/> and
/// <see cref="Instructions"/> while it is still building the service collection, because they are baked
/// into the MCP server's registration - a plain <c>Configure</c> runs too late for those. It would still
/// take effect for <see cref="RoutePattern"/> and <see cref="AuthenticationSchemes"/>, which are read
/// later at endpoint-mapping time, and that is the trap: a host using <c>Configure</c> gets a partially
/// applied configuration and no error at all. One rule for all five avoids it.
/// </para>
/// </summary>
public class SiteMcpOptions
{
    /// <summary>
    /// Where the Streamable HTTP endpoint is mapped. Defaults to <c>/mcp</c>, the convention clients
    /// assume when a user pastes a bare origin.
    /// </summary>
    public string RoutePattern { get; set; } = "/mcp";

    /// <summary>The server name reported in the MCP <c>initialize</c> handshake.</summary>
    public string ServerName { get; set; } = "Dignite.Site";

    /// <summary>The server version reported in the MCP <c>initialize</c> handshake.</summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// The authentication schemes the MCP endpoint authorizes against. Empty means the application's
    /// default policy.
    /// <para>
    /// <b>A host that also serves an interactive UI must set this, and the default is wrong for it.</b>
    /// In an ABP MVC host the default challenge scheme is the Identity application cookie, so an
    /// unauthenticated MCP request is answered with a <c>302</c> to the login page - which an MCP client
    /// cannot act on. It needs a <c>401</c> carrying
    /// <c>WWW-Authenticate: Bearer resource_metadata="..."</c>, which is how it discovers where to
    /// authenticate (RFC 9728).
    /// </para>
    /// <para>
    /// The usual value is a single entry, <c>McpAuthenticationDefaults.AuthenticationScheme</c>. That
    /// scheme authenticates nothing on its own - it <i>forwards</i>, so the host also points its
    /// <c>ForwardAuthenticate</c> at whichever bearer scheme it runs (this solution's does so in
    /// <c>HostModule.ConfigureMcpResourceMetadata</c>) - and it is what produces the 401 challenge. Which
    /// bearer scheme a host runs is the host's business, which is why this list is left to the host to
    /// state rather than guessed at here.
    /// </para>
    /// </summary>
    public List<string> AuthenticationSchemes { get; } = new();

    /// <summary>
    /// Instructions handed to the client on connect - the one place to tell a model how this server
    /// wants to be used, before it has called anything.
    /// <para>
    /// It earns its length: the tool surface is cut by entity and addressed by name (总体设计 §6.2), so a
    /// client that does not first read the schema will guess at page and content type names that are the
    /// tenant's runtime data. Saying so here costs one message and saves a round trip of wrong guesses.
    /// </para>
    /// </summary>
    public string Instructions { get; set; } =
        "This server manages one website's content.\n\n" +
        "Start by reading the `site://schema` resource, or calling `get_site_schema` if your client " +
        "does not support resources. It returns the site's languages, its pages, the content types " +
        "under each page, and the fields of each content type - all addressed by name. Every other " +
        "tool takes those names; none of them takes an id.\n\n" +
        "Writing content: pick the content type whose description matches what the user asked for " +
        "(\"post a news item\" is a content type named by the tenant, not by this server), then supply a " +
        "value for every field the schema marks required, keyed by the field's `name`.\n\n" +
        "Two things that are easy to get wrong:\n" +
        "- `slug` is always required. Pass an empty string only when the content IS the page (a home or " +
        "\"about\" page whose URL is the page's route). Otherwise pass a real slug.\n" +
        "- `cultureName` must be one of the schema's `enabledLanguages`. Do not invent a variant.";
}
