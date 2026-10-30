```json
//[doc-seo]
{
    "Description": "Dignite Site is a multi-tenant CMS on the ABP Framework: build pages and publish content by talking to your own AI client over MCP, with SEO, sitemaps, feeds and multilingual sites built in."
}
```

# Dignite Site

Dignite Site is a **multi-tenant CMS** for the [ABP Framework](https://abp.io). Instead of filling in forms in an admin backend, you build pages and publish content by talking to your own AI client: it calls the tools of Site's [Model Context Protocol](https://modelcontextprotocol.io) server, and Site validates what it writes against the site's field definitions. Every tenant has one site, with its own pages, fields, contents, languages and files.

Site is the AI-native successor of [Dignite.Cms](https://github.com/dignite-projects/cms). It keeps the core of a CMS (field definitions, multi-tenancy, multilingual content) and adds what a site needs around its content: SEO metadata, sitemaps, feeds, `robots.txt`, `llms.txt` and a primary domain for its canonical URLs.

## Core Ideas

* **Everything is a page, and a page is a route.** The pages of a site are its authoritative route table. The sitemap, the canonical URLs and the hreflang alternates are all derived from it, whatever renders the pages.
* **AI works through MCP; Site does not ship an assistant.** You describe what you want in your own AI client, such as "create a page at `/blog`" or "publish this news post", and it calls Site's MCP tools. Building a site and running it later take the same path.
* **The schema is the contract.** The field definitions of a site are both the validation gate for every write and the target shape handed to the AI client.

## Features

* **[Pages and routing](pages-and-routing.md)**: A page owns a route template, such as `/blog/{slug}` or `/news/{publishTime:yyyy}/{slug}`. A request is resolved to a page and, where the route has one, to a content.
* **[Content types and fields](content-types-and-fields.md)**: A tenant defines its fields once, in a field library, and arranges them into the content types of each page. Fields are [FlexFields](https://github.com/dignite-projects/abp-modules) field types, from text, numbers and dates to Matrix, Table and CKEditor rich text, plus Site's own [content reference, file and SEO types](field-types.md).
* **[Contents](contents.md)**: A content belongs to a page and a content type, has a slug and a language, and is either a draft or published. A published content with a future publish time goes live at that time.
* **[SEO and discoverability](seo/index.md)**: Head metadata, canonical URLs and hreflang alternates for every page, a sharded sitemap, RSS, Atom and JSON feeds, `robots.txt` with switches for search indexing, AI training and AI search crawlers, and an optional `llms.txt`.
* **[Multilingual sites](rendering/languages.md)**: The site's languages, with the default one at the root and the others under a language prefix, and helpers for links, a language switcher and [template texts](rendering/template-localization.md).
* **[Files](files.md)**: A file library for the images and attachments of contents, with upload rules, relative addresses and on-the-fly image resizing.
* **[Rendering](rendering/templates.md)**: `Dignite.Site.Public.Web` renders a site in an ASP.NET Core MVC application, from the application's Razor templates; each tenant can have templates of its own under `/Sites/{tenantName}/`.
* **[MCP server](mcp/index.md)**: Tools to read the site's schema and to manage its pages, content types, fields, contents and files, authorized by the same permissions as the administration API.
* **[Angular admin UI](admin-ui.md)**: `@dignite/ng.site` manages the same things in an ABP Angular application.

[Getting Started](getting-started.md) installs Site into an ABP application and builds a first page from an AI client.

## Packages

| Package | Description |
|---|---|
| `Dignite.Site.Domain.Shared` | Constants, enums, setting names, localization and error codes. |
| `Dignite.Site.Domain` | Pages, content types, fields, contents, the file library, routing, and the builders of the head metadata, sitemap and feeds. |
| `Dignite.Site.EntityFrameworkCore` | The database provider: the Entity Framework Core repositories, and `ConfigureSite()` for your `DbContext`. |
| `Dignite.Site.MongoDB` | The MongoDB repositories of the file library, and nothing else: pages, content types, fields and contents have no MongoDB implementation, so Site needs an Entity Framework Core database. |
| `Dignite.FlexFields.Site` | Site's own field types: content reference, file and SEO. |
| `Dignite.Site.Common.*` | The contracts and services that the administration and public sides share. |
| `Dignite.Site.Admin.*` | The administration API (`/api/site-admin/`): pages, content types, fields, contents and files. Protected by the `SiteAdmin.*` permissions and the `Site.Enable` feature. |
| `Dignite.Site.Public.*` | The anonymous read API (`/api/site-public/`): path resolution, published contents, head metadata, sitemaps, feeds and files. |
| `Dignite.Site.Application`, `.Application.Contracts`, `.HttpApi`, `.HttpApi.Client` | The administration and public sides together, for an application that hosts both. |
| `Dignite.Site.Public.Web` | Renders the site in an ASP.NET Core MVC application: the catch-all route, the `/Sites/` templates, tag helpers, `sitemap.xml`, the feeds, `robots.txt`, `llms.txt` and the language helpers. |
| `Dignite.Site.Mcp` | The MCP tools and resources of Site, for the [MCP server](https://github.com/dignite-projects/abp-modules) of abp-modules. |
| `Dignite.Site.Installer` | Installation metadata for the ABP tooling. |
| `@dignite/ng.site` (npm) | The Angular admin UI. |

## Requirements

* .NET 10 SDK
* ABP Framework 10.7 or later
* [Dignite ABP Modules](https://github.com/dignite-projects/abp-modules) 10.7 or later, which the Site packages depend on
* Angular 22, for `@dignite/ng.site`

## Versioning

Dignite Site follows [Semantic Versioning](https://semver.org) with its own numbers; unlike Dignite ABP Modules, it does not track ABP's version. All the NuGet packages (`Dignite.Site.*`, `Dignite.FlexFields.Site`) and the npm package (`@dignite/ng.site`) always share one version.

| Dignite Site | ABP Framework | Dignite ABP Modules | Angular |
|---|---|---|---|
| 2.x | 10.7+ | 10.7+ | 22.x |
| 1.x | 10.7+ | 10.7+ | 22.x |

* **Major**: Anything that makes an upgrade more than a package bump: a removed or changed public .NET API, `@dignite/ng.site` export or component input or output; an HTTP API or DTO field removed or changed in meaning; an incompatible MCP tool parameter; a database migration that needs manual data work; or a higher minimum ABP major version.
* **Minor**: Backward-compatible features: new field types, MCP tools and APIs, additive database migrations, or a higher minimum ABP minor version.
* **Patch**: Bug fixes only. A patch release never carries a database migration.

Stable releases are published to NuGet.org and npm. The changes of every release are listed in the [CHANGELOG](https://github.com/dignite-projects/site/blob/main/CHANGELOG.md).

## Source Code

The source code is on [GitHub](https://github.com/dignite-projects/site) and licensed under the [MIT License](https://github.com/dignite-projects/site/blob/main/LICENSE). Issues and pull requests are welcome.
