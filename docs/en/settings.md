```json
//[doc-seo]
{
    "Description": "Configure a Dignite Site: the Site.* settings for languages, primary domain, robots, llms.txt and branding, the Site.Enable feature, and the SiteAdmin permissions."
}
```

# Settings, Features and Permissions

A site has no configuration entity of its own: every tenant is one site, and its configuration is a set of [ABP settings](https://abp.io/docs/latest/framework/infrastructure/settings), a [feature](https://abp.io/docs/latest/framework/infrastructure/features) and the [permissions](https://abp.io/docs/latest/framework/fundamentals/authorization) of its users. The host's own site, with no tenant, is configured the same way.

## Settings

| Setting | Default | Description |
|---|---|---|
| `Site.EnabledLanguages` | `en` | The site's languages, comma-separated, such as `en,ja,zh-Hans`. The first one is the default language, with no URL prefix; the others are served under `/{culture}/`. See [Multilingual Sites](rendering/languages.md). |
| `Site.PrimaryDomain` | Empty | The site's absolute address, such as `https://www.example.com`, optionally with a base path. See [Primary Domain](#primary-domain). |
| `Site.Robots.AllowIndexing` | `true` | Whether search engines may index the site. |
| `Site.Robots.AllowAiTraining` | `false` | Whether crawlers that train AI models (GPTBot, Google-Extended, ClaudeBot, CCBot) may crawl the site. |
| `Site.Robots.AllowAiSearch` | `true` | Whether crawlers that power AI search answers (OAI-SearchBot, PerplexityBot, Claude-SearchBot) may crawl the site. |
| `Site.EnableLlmsTxt` | `false` | Whether the site serves an `/llms.txt` index of its pages. |
| `Site.Branding.AppName` | Empty | The site's display name. Empty uses the application's default. |
| `Site.Branding.LogoUrl` | Empty | The logo shown on a light background. |
| `Site.Branding.LogoReverseUrl` | Empty | The logo shown on a dark background. |

The `Site.Robots.*` and `Site.EnableLlmsTxt` settings are described in [robots.txt and llms.txt](seo/robots-and-llms-txt.md). The branding settings replace ABP's branding provider, so the theme of an ABP application shows each tenant's own name and logo.

Every setting can be set at any level: a value in the host's `appsettings.json` is the default of all sites, and a tenant can override it.

````json
{
  "Settings": {
    "Site.EnabledLanguages": "en,ja",
    "Site.Robots.AllowAiTraining": "false"
  }
}
````

To set a tenant's value from code, use ABP's `ISettingManager`:

````csharp
await _settingManager.SetForTenantAsync(tenantId, SiteSettings.PrimaryDomain, "https://www.example.com");
````

A change takes effect on the next request.

### Primary Domain

The sitemap, the feeds, `robots.txt`, the canonical URLs and the hreflang alternates are built on `Site.PrimaryDomain`, never on the host name a request happened to arrive on. A site reachable at both a custom domain and a platform subdomain therefore names one of them as the original, instead of both competing as duplicate content.

When the setting is empty, an application with `Dignite.Site.Public.Web` uses the scheme and host of each request instead. Without it, building an absolute URL fails with `Site:050001`, so an API-only deployment that serves the head metadata should set the primary domain of every site.

Site does not map host names to tenants. Resolve the tenant of a request with ABP's [domain tenant resolver](https://abp.io/docs/latest/framework/architecture/multi-tenancy), or with your own resolver for custom domains.

## The Site Feature

The `Site.Enable` feature, on by default, switches the administration side of Site for a tenant or an edition. While it is off:

* The `SiteAdmin.*` permissions read as not granted, so the Site menu of the admin UI disappears and the administration API and the MCP tools refuse every call.
* Stored permission grants are kept, and work again when the feature is turned back on.
* The public side keeps running: the site is still rendered, and its files, sitemap and feeds are still served.

## Permissions

| Permission | Grants |
|---|---|
| `SiteAdmin.Pages` | Reading the pages. |
| `SiteAdmin.Pages.Create`, `.Update`, `.Delete` | Creating, updating and deleting pages. |
| `SiteAdmin.ContentTypes` | Reading the content types. |
| `SiteAdmin.ContentTypes.Create`, `.Update`, `.Delete` | Creating, updating and deleting content types. |
| `SiteAdmin.Fields` | Reading the field library. |
| `SiteAdmin.Fields.Create`, `.Update`, `.Delete` | Creating, updating and deleting fields. |
| `SiteAdmin.Fields.Rename` | Renaming a field, which migrates the stored values of every content. |
| `SiteAdmin.Contents` | Reading the contents, drafts included, and managing every user's files. |
| `SiteAdmin.Contents.Create`, `.Update`, `.Delete` | Creating, updating and deleting contents, and uploading, changing and deleting files. |
| `SiteAdmin.Templates` | Reading the [Scriban templates](rendering/scriban-templates.md) and their versions. |
| `SiteAdmin.Templates.Create`, `.Update`, `.Delete` | Creating, saving and deleting templates. `.Update` also covers publishing a version, and so rolling back. |

The file library has no permissions of its own; see [Files](files.md#permissions). The public read API needs no permission. The MCP tools are authorized by the same permissions as the administration API; see [MCP Server](mcp/index.md#authorization).

## See Also

* [Multilingual Sites](rendering/languages.md)
* [robots.txt and llms.txt](seo/robots-and-llms-txt.md)
* [MCP Server](mcp/index.md)
