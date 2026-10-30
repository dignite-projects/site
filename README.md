# Dignite Site

Multi-tenant CMS on [ABP](https://abp.io) — build pages and publish content by talking to your AI client via MCP, no admin backend forms.

Dignite Site is the AI-native next generation of [Dignite.Cms](https://github.com/dignite-projects/cms): it keeps the same proven core (field definitions, multi-tenancy, multi-language, versioning) but replaces the content authoring interface — from a backend form to an **MCP** server your own AI client talks to — and adds the site-level capabilities Cms never had (SEO metadata, sitemaps, feeds, `robots.txt` and `llms.txt`, a primary domain for canonical URLs).

## Core ideas

- **Everything is a page, and a page is a route.** The backend's page collection is the authoritative route table — sitemap, canonical URLs, and hreflang all derive from it.
- **AI works through MCP, the platform doesn't ship its own assistant.** You describe what you want ("create a page at `/blog`", "publish a news post") in your own AI client; it calls MCP tools, the platform validates against field definitions and persists it.
- **Schema is the contract.** Field definitions are both the validation gate for writes and the target shape handed to AI.

## Documentation

**[Read the documentation](https://github.com/dignite-projects/site/blob/main/docs/en/index.md)**, or go straight to a topic:

- [Getting Started](https://github.com/dignite-projects/site/blob/main/docs/en/getting-started.md) - install Site into an ABP application and build a first page from an AI client
- [Pages and Routing](https://github.com/dignite-projects/site/blob/main/docs/en/pages-and-routing.md), [Content Types and Fields](https://github.com/dignite-projects/site/blob/main/docs/en/content-types-and-fields.md), [Contents](https://github.com/dignite-projects/site/blob/main/docs/en/contents.md) - the content model
- [Templates](https://github.com/dignite-projects/site/blob/main/docs/en/rendering/templates.md), [Scriban Templates](https://github.com/dignite-projects/site/blob/main/docs/en/rendering/scriban-templates.md), [Multilingual Sites](https://github.com/dignite-projects/site/blob/main/docs/en/rendering/languages.md), [Localizing Template Texts](https://github.com/dignite-projects/site/blob/main/docs/en/rendering/template-localization.md) - rendering a site
- [Files](https://github.com/dignite-projects/site/blob/main/docs/en/files.md) - the file library for images and attachments
- [SEO](https://github.com/dignite-projects/site/blob/main/docs/en/seo/index.md) - head metadata, sitemap, feeds, `robots.txt` and `llms.txt`
- [MCP Server](https://github.com/dignite-projects/site/blob/main/docs/en/mcp/index.md) - the tools an AI client works with, and [how to connect one](https://github.com/dignite-projects/site/blob/main/docs/en/mcp/connecting-clients.md)

The design documents for contributors are in [`dev-docs/`](https://github.com/dignite-projects/site/tree/main/dev-docs); the overall design, [`00-总体设计.md`](https://github.com/dignite-projects/site/blob/main/dev-docs/00-总体设计.md), is in Chinese.

## Project structure

| Path | Description |
|---|---|
| `src/` | ABP domain, application, and HTTP API layers (`Dignite.Site.*`, plus `Admin.*` / `Public.*` sub-apps) |
| `host/` | ASP.NET Core host application |
| `angular/` | Angular-based admin UI |
| `test/` | Unit and integration tests |
| `docs/` | User documentation, in the [ABP Docs module](https://abp.io/docs/latest/modules/docs) format |
| `dev-docs/` | Design documents for contributors (not published) |

## Getting started

To use Dignite Site in your own ABP application, follow [Getting Started](https://github.com/dignite-projects/site/blob/main/docs/en/getting-started.md) in the documentation. The rest of this section is for building this repository.

Install the .NET 10 SDK, then restore and build from the repository root:

```sh
dotnet restore Dignite.Site.slnx
dotnet build Dignite.Site.slnx --no-restore --configuration Release
```

The .NET dependencies from [abp-modules](https://github.com/dignite-projects/abp-modules)
(FlexFields, BLOB storage and the MCP server) are NuGet packages from nuget.org, pinned to
`10.7.1` by `DigniteAbpModulesVersion` in `common.props`. No sibling `abp-modules`
checkout or package registry credentials are needed. Local builds, PR CI and release builds
use the same package references. See [angular/README.md](https://github.com/dignite-projects/site/blob/main/angular/README.md) for the admin UI setup.

This is an [ABP Framework](https://abp.io) solution. See the [ABP documentation](https://docs.abp.io) for general setup, and `host/Dignite.Site.Host/migrate-database.ps1` for database migration.

## Versioning

Dignite Site follows [Semantic Versioning](https://semver.org) with its own numbers: unlike
[abp-modules](https://github.com/dignite-projects/abp-modules), it does not track ABP's version.
The NuGet packages (`Dignite.Site.*`) and the npm package (`@dignite/ng.site`) always share one version.

| Dignite Site | ABP | abp-modules | Angular |
|---|---|---|---|
| 1.x | 10.7+ | 10.7+ | 22.x |

What counts as a major, minor or patch change is spelled out in
[Versioning](https://github.com/dignite-projects/site/blob/main/docs/en/index.md#versioning). A patch release never carries a database migration.

Pre-releases are `x.y.z-preview.N` and are published to GitHub Packages only; stable versions go to
nuget.org and npmjs. See [CHANGELOG.md](https://github.com/dignite-projects/site/blob/main/CHANGELOG.md) for each release.

## License

[MIT](https://github.com/dignite-projects/site/blob/main/LICENSE)
