# Dignite Site

Multi-tenant CMS on [ABP](https://abp.io) — build pages and publish content by talking to your AI client via MCP, no admin backend forms.

Dignite Site is the AI-native next generation of [Dignite.Cms](https://github.com/dignite-projects/cms): it keeps the same proven core (field definitions, multi-tenancy, multi-language, versioning) but replaces the content authoring interface — from a backend form to an **MCP** server your own AI client talks to — and adds the site-level capabilities Cms never had (SEO, sitemap, feed, custom domains).

## Core ideas

- **Everything is a page, and a page is a route.** The backend's page collection is the authoritative route table — sitemap, canonical URLs, and hreflang all derive from it.
- **AI works through MCP, the platform doesn't ship its own assistant.** You describe what you want ("create a page at `/blog`", "publish a news post") in your own AI client; it calls MCP tools, the platform validates against field definitions and persists it.
- **Schema is the contract.** Field definitions are both the validation gate for writes and the target shape handed to AI.

See [docs/00-总体设计.md](docs/00-总体设计.md) for the full design (in Chinese).

A site's own template texts (option names, labels, archive titles) are localized per tenant, with JSON files next to the templates. See [docs/template-localization.md](docs/template-localization.md).

For a multilingual site, templates and the host's layout get the site's language rules: in-site links in the page's language, a language switcher, and an optional redirect of `/` to the visitor's language. See [docs/site-languages.md](docs/site-languages.md).

Site has its own file library - the images and attachments content fields use, uploaded from the admin UI, over MCP or from a CKEditor field, and served to visitors from `/api/site-public/files/`. Permissions ride on the Contents permissions. See [docs/site-files.md](docs/site-files.md).

## Project structure

| Path | Description |
|---|---|
| `src/` | ABP domain, application, and HTTP API layers (`Dignite.Site.*`, plus `Admin.*` / `Public.*` sub-apps) |
| `host/` | ASP.NET Core host application |
| `angular/` | Angular-based admin UI |
| `test/` | Unit and integration tests |
| `docs/` | Design documentation |

## Getting started

Install the .NET 10 SDK, then restore and build from the repository root:

```sh
dotnet restore Dignite.Site.slnx
dotnet build Dignite.Site.slnx --no-restore --configuration Release
```

The .NET dependencies from [abp-modules](https://github.com/dignite-projects/abp-modules)
(FlexFields, BLOB storage and the MCP server) are NuGet packages from nuget.org, pinned to
`10.0.0-rc.29` by `DigniteAbpModulesVersion` in `common.props`. No sibling `abp-modules`
checkout or package registry credentials are needed. Local builds, PR CI and release builds
use the same package references. See [angular/README.md](angular/README.md) for the admin UI setup.

This is an [ABP Framework](https://abp.io) solution. See the [ABP documentation](https://docs.abp.io) for general setup, and `host/Dignite.Site.Host/migrate-database.ps1` for database migration.

## License

[MIT](LICENSE)
