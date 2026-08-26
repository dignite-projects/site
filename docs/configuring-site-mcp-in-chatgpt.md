# Configuring the Site MCP in ChatGPT

This document explains how to configure the local Site MCP server in the ChatGPT/Codex desktop application through `config.toml`.

The example assumes that the Site MCP endpoint is:

```text
https://localhost:44315/mcp
```

## 1. Choose the configuration file

Use one of these locations:

- Global configuration: `C:\Users\<username>\.codex\config.toml`
- Project configuration: `<project>\.codex\config.toml`

Use the global file when the `site` MCP should be available to all projects. Use the project file when it should only be available to one project. Keep one authoritative `site` entry to avoid configuration conflicts.

The file must be named exactly `config.toml`. A `.mcp.json` file is not a substitute for the Codex desktop configuration file.

## 2. Complete Site MCP configuration

Add this block to `config.toml`:

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
NODE_OPTIONS = "--use-system-ca"
NODE_TLS_REJECT_UNAUTHORIZED = "0"
```

## 3. Meaning of each setting

### `[mcp_servers.site]`

Defines an MCP server named `site`. The name is the identifier used by the desktop application and by commands such as:

```text
codex mcp list
```

### `enabled = true`

Enables this MCP server. Set it to `false` to keep the configuration but temporarily disable the connection.

### `startup_timeout_sec = 360`

Allows up to 360 seconds for the MCP connection to start. This is intentionally longer than the default because the first OAuth connection may open a browser sign-in flow and install the `mcp-remote` package through `npx`.

### `command = "npx"`

Starts the Node package runner. The `npx` executable must be available in the environment used by the ChatGPT/Codex desktop application.

### `mcp-remote@0.1.38`

Runs the `mcp-remote` bridge. The Site server is an HTTPS Streamable HTTP server, while the local desktop configuration starts the bridge as a command process. `mcp-remote` connects those two sides.

The version is pinned so that the connection behavior does not change unexpectedly after a package update.

### `https://localhost:44315/mcp`

The Site MCP endpoint. The `/mcp` path is required; the Site home page at `/` is not the MCP endpoint.

### `--transport http-only`

Forces the bridge to use HTTP transport. This matches the Site MCP endpoint and avoids transport auto-detection ambiguity.

### `--static-oauth-client-info`

Provides the OAuth client identifier expected by the Site host:

```json
{"client_id":"Site_Mcp"}
```

### `--static-oauth-client-metadata`

Declares the OAuth scope requested by the Site MCP client:

```json
{"scope":"Host offline_access"}
```

`Host` is the Site host API scope. `offline_access` allows the bridge to retain a refresh token so that the user does not need to sign in for every connection.

### `--auth-timeout 300`

Gives the browser-based OAuth flow up to 300 seconds to complete.

### `[mcp_servers.site.env]`

Provides environment variables only to the Site MCP process.

`NODE_OPTIONS = "--use-system-ca"` tells Node.js to use the system certificate store.

`NODE_TLS_REJECT_UNAUTHORIZED = "0"` disables TLS certificate verification. This is only suitable for the local development server using a self-signed certificate. It should be removed when the local certificate is trusted or when the server uses a valid certificate.

## 4. Restart and authenticate

After saving `config.toml`:

1. Fully quit and restart the ChatGPT/Codex desktop application.
2. The first connection may open a Site sign-in page in the browser.
3. Sign in to the Site account and allow the local OAuth callback to complete.
4. Return to the ChatGPT/Codex application.

The OAuth flow is handled by `mcp-remote`. Therefore, this command is not normally the right way to authenticate this configuration:

```text
codex mcp login site
```

That command expects a directly configured Streamable HTTP MCP server. In this setup, the configured command is the local `mcp-remote` bridge, which performs OAuth itself.

## 5. Verify the configuration

From the project directory, run:

```text
codex mcp list
```

The `site` entry should be enabled and should show the `npx mcp-remote` command pointing to `https://localhost:44315/mcp`.

The list may show `Auth Unsupported` because it is describing the local command wrapper, not the OAuth capability of the remote Site server. A successful browser sign-in and MCP connection are the meaningful authentication checks.

Once connected, the Site MCP should expose the `site://schema` resource. That resource contains the Site's enabled languages, pages, content types, and fields.

## 6. Troubleshooting

### The `site` server does not appear

- Confirm that the block is in `config.toml`, not `.mcp.json`.
- Confirm the section is named exactly `[mcp_servers.site]`.
- Restart the desktop application completely.
- Run `codex mcp list` from the intended project directory.

### The browser sign-in does not start

- Confirm that `https://localhost:44315/` is reachable in the browser.
- Confirm that the Site account is already signed in.
- Check that `--static-oauth-client-info` uses `Site_Mcp`.
- Check that the requested scope is `Host offline_access`.

### TLS or certificate errors appear

For the local self-signed certificate, verify that these environment settings are present:

```toml
[mcp_servers.site.env]
NODE_OPTIONS = "--use-system-ca"
NODE_TLS_REJECT_UNAUTHORIZED = "0"
```

For a trusted certificate, remove `NODE_TLS_REJECT_UNAUTHORIZED = "0"`.

### The connection starts but Site resources are unavailable

- Confirm that the endpoint ends with `/mcp`.
- Confirm that the Site application is running on port `44315`.
- Confirm that the signed-in user has permission to access Site pages, content types, fields, and content.

## 7. Security notes

- Treat `config.toml` as a local configuration file and do not commit personal OAuth tokens or unrelated private settings.
- The `NODE_TLS_REJECT_UNAUTHORIZED = "0"` setting disables certificate verification and should remain limited to local development.
- Use a trusted certificate and remove the TLS bypass before using a non-local or production Site endpoint.
