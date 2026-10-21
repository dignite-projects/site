```json
//[doc-seo]
{
    "Description": "Connect AI clients such as Claude Code, ChatGPT and MCP Inspector to the Dignite Site MCP server: protected resource metadata, OpenIddict resources and clients, and troubleshooting OAuth."
}
```

# Connecting AI Clients

An AI client connects to the MCP server at `/mcp` and signs in with OAuth, as a user of the site. The application publishes where to sign in, the authorization server accepts the MCP server as a resource, and an OAuth client is registered for the AI clients. This page sets up all three on an application with ABP's OpenIddict, then connects some common clients.

The [host application in the repository](https://github.com/dignite-projects/site/tree/main/host/Dignite.Site.Host) is configured this way: see `ConfigureMcpResourceMetadata` in `SiteHostModule` and `CreateMcpApplicationsAsync` in `Data/OpenIddictDataSeedContributor.cs`.

## The Server

### Protected Resource Metadata

A client pointed at `/mcp` without a token has to find out where to sign in. Publish the [RFC 9728](https://datatracker.ietf.org/doc/html/rfc9728) protected resource metadata, in the `ConfigureServices` method of the module that hosts the server:

````csharp
var configuration = context.Services.GetConfiguration();

context.Services.AddAbpMcpAuthenticationDiscovery(metadata =>
{
    metadata.Resource = configuration["App:SelfUrl"]!;
    metadata.AuthorizationServers.Add(configuration["AuthServer:Authority"]!);
    metadata.ScopesSupported.Add("MyProject");
});
````

It serves the metadata at `/.well-known/oauth-protected-resource`, and at `/.well-known/oauth-protected-resource/mcp` for the endpoint itself, and makes an unauthenticated request to `/mcp` answer `401` with a `WWW-Authenticate` header that points at the latter, instead of the redirect to the login page that an MVC application answers with. `Resource` is the application's own address; `ScopesSupported` is the scope of your API.

### The Resource

Following the MCP specification, a client sends the resource it discovered back to the authorization server as the [RFC 8707](https://datatracker.ietf.org/doc/html/rfc8707) `resource` parameter. OpenIddict refuses a resource it does not know, so register the application's address:

````csharp
public override void PreConfigureServices(ServiceConfigurationContext context)
{
    var selfUrl = context.Services.GetConfiguration()["App:SelfUrl"]!.TrimEnd('/');

    PreConfigure<OpenIddictServerBuilder>(builder =>
    {
        builder.RegisterResources(new Uri(selfUrl, UriKind.Absolute));
    });
}
````

### The OAuth Client

Register one OAuth client that interactive AI clients sign in with. A desktop AI client cannot keep a secret and receives the code on a loopback address with a port it picks at run time, so the client is:

| Property | Value |
|---|---|
| Client id | A name of your choice, such as `MyProject_Mcp`. Users enter it in their AI client. |
| Client type | `public`, with no secret. |
| Application type | `native`. OpenIddict ignores the port of a loopback redirect URI only for a native application. |
| Grant types | `authorization_code` and `refresh_token`. |
| PKCE | Required. |
| Consent | `explicit`: the user sees what a third-party client is being granted. |
| Redirect URIs | `http://localhost/callback`, `http://127.0.0.1/callback`, `http://[::1]/callback`, and the same three with `/oauth/callback`, which MCP Inspector uses. |
| Scopes | Your API scope and `offline_access`, plus `profile`, `email` and `roles` as your application needs. |
| Resource permissions | `rsrc:` followed by the application's address, once without and once with a trailing slash, since some clients send it back with one. |

The tokens it gets are ordinary access tokens of your API: the same permissions, the same tenant claim, the same checks as the HTTP API.

Dynamic Client Registration is not used: the MCP specification deprecates it, and a pre-registered client is its first choice. Clients that cannot discover the client id ask the user for it.

### Unattended Clients

A job that publishes without a user, from CI for example, uses a second, confidential client with the `client_credentials` grant, your API scope and the same resource permissions. Its token carries no user, so ABP checks the permissions granted to the client itself. Seed it with none, and grant it only the `SiteAdmin.*` permissions the job needs, in the permission management of the `C` (client) provider.

### Browser-Based Clients

A client that runs in a browser, such as MCP Inspector, calls the discovery document and the token endpoint from the page, so add its origin to the application's CORS origins, `App:CorsOrigins` in an ABP application. The MCP endpoint itself is called by the client's local proxy and needs no CORS.

## Clients

The examples connect to `https://www.example.com/mcp` with the client id `MyProject_Mcp` and the API scope `MyProject`.

### Claude Code

Add the server with the client id, then sign in from Claude Code with `/mcp`:

````bash
claude mcp add --transport http --client-id MyProject_Mcp site https://www.example.com/mcp
````

Claude Code receives the code at `http://localhost:{port}/callback`, which the redirect URIs above accept on any port.

### ChatGPT and Codex

The ChatGPT and Codex desktop applications read MCP servers from Codex's `config.toml`. Connect through the [`mcp-remote`](https://www.npmjs.com/package/mcp-remote) bridge, which handles the OAuth flow, by adding to `~/.codex/config.toml`, or to `.codex/config.toml` in a project:

````toml
[mcp_servers.site]
enabled = true
startup_timeout_sec = 360
command = "npx"
args = [
  "-y",
  "mcp-remote@0.1.38",
  "https://www.example.com/mcp",
  "--transport",
  "http-only",
  "--static-oauth-client-info",
  '{"client_id":"MyProject_Mcp"}',
  "--static-oauth-client-metadata",
  '{"scope":"MyProject offline_access"}',
  "--auth-timeout",
  "300"
]
````

* `startup_timeout_sec` leaves time for the first sign-in in the browser and for `npx` to install the bridge.
* `--static-oauth-client-info` gives the bridge the pre-registered client id.
* `--static-oauth-client-metadata` requests your API scope, and `offline_access` for a refresh token, so the user does not sign in on every start.
* The version of `mcp-remote` is pinned, so the connection does not change with a package update.

Restart the application after saving the file; the first connection opens the sign-in page in the browser. `codex mcp list` lists the server, and may show "Auth Unsupported" for it: it describes the local bridge, not the server. Do not use `codex mcp login` for this configuration; the bridge signs in itself.

### MCP Inspector

[MCP Inspector](https://github.com/modelcontextprotocol/inspector) is the reference client for testing a server:

````bash
npx @modelcontextprotocol/inspector
````

Choose the **Streamable HTTP** transport, enter the URL of `/mcp`, and the client id in the authentication settings. Inspector receives the code at `/oauth/callback`, and its page runs at `http://localhost:6274`, which has to be a [CORS origin](#browser-based-clients).

### Other Clients

Any client that supports remote MCP servers with OAuth and a pre-registered client id can connect. A client that receives the code on a web address of its own, rather than a loopback address, needs that address added to the OAuth client's redirect URIs; its documentation names it.

## Local Development

Node.js rejects the ASP.NET Core development certificate, even where the operating system trusts it. To connect a Node-based client or bridge, such as `mcp-remote` or MCP Inspector, to a local application on HTTPS, turn certificate checks off for that process only:

````toml
[mcp_servers.site.env]
NODE_TLS_REJECT_UNAUTHORIZED = "0"
````

Never use this against a server reachable from elsewhere.

## Troubleshooting

| Symptom | Cause |
|---|---|
| `/mcp` answers `302` to the login page | `AddAbpMcpAuthenticationDiscovery` is not called, so the client cannot discover where to sign in. |
| The token request fails with `invalid_target` (`ID2190`) | The resource the client sends is not registered with `RegisterResources`. |
| The token request fails with `ID2192` | The OAuth client lacks the `rsrc:` permission for the resource, possibly for the form with a trailing slash. |
| The authorization fails with an invalid redirect URI | The OAuth client is not `native`, or the callback path the client uses is not registered. |
| The client connects, but lists no `site_*` tools | The user has no `SiteAdmin.*` permission, or the `Site.Enable` feature is off for the tenant. |
| `site_get_schema` is refused | It needs `SiteAdmin.Pages`, `SiteAdmin.ContentTypes` and `SiteAdmin.Fields` together. |

## See Also

* [MCP Server](index.md)
* [Tools](tools.md)
* [Hosting the Server](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/aspnetcore-mcp/hosting.md)
