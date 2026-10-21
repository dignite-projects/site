# Connecting an AI client to the local host

How to connect an AI client to an MCP server is documented for users in
[docs/en/mcp/connecting-clients.md](../docs/en/mcp/connecting-clients.md). For this repository's host
(`host/Dignite.Site.Host`), use these values in its examples:

| Value | Local host |
|---|---|
| MCP endpoint | `https://localhost:44315/mcp` |
| Client id (interactive clients) | `Site_Mcp` (`SiteHostConsts.McpClientId`) |
| Client id (client credentials) | `Site_Mcp_Service`, its secret from `OpenIddict:Applications:Site_Mcp_Service:Secret`; seeded with no permissions |
| API scope | `Host` |

Both clients are seeded by `Data/OpenIddictDataSeedContributor.cs`, with the loopback redirect URIs and the
`rsrc:` permissions for `App:SelfUrl`.

The development certificate is not trusted by Node.js, so a Node-based client or bridge (`mcp-remote`, MCP
Inspector) needs `NODE_TLS_REJECT_UNAUTHORIZED=0` against this host - `--use-system-ca` and
`NODE_EXTRA_CA_CERTS` do not help with this certificate. MCP Inspector's page, `http://localhost:6274`, also
has to be in `App:CorsOrigins`.

For Codex, the `config.toml` block of the user page with these values:

```toml
[mcp_servers.site]
enabled = true
startup_timeout_sec = 360
command = "npx"
args = [
  "-y",
  "mcp-remote@0.1.38",
  "https://localhost:44315/mcp",
  "--transport",
  "http-only",
  "--static-oauth-client-info",
  '{"client_id":"Site_Mcp"}',
  "--static-oauth-client-metadata",
  '{"scope":"Host offline_access"}',
  "--auth-timeout",
  "300"
]

[mcp_servers.site.env]
NODE_TLS_REJECT_UNAUTHORIZED = "0"
```
