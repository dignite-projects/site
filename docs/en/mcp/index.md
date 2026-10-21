```json
//[doc-seo]
{
    "Description": "The MCP server of Dignite Site: let an AI client build and run a site through site_* tools and the site://schema resource, addressed by name and authorized by the SiteAdmin permissions."
}
```

# MCP Server

Site does not ship an AI assistant. It opens its authoring capabilities as tools of a [Model Context Protocol](https://modelcontextprotocol.io) server, so the AI client a user already has, such as Claude or ChatGPT, can build and run a site from a conversation: "create a page at `/blog`", "publish this news post", "upload this image as the cover".

`SiteMcpModule` contributes Site's tools to the application's MCP server, which the [MCP Server module of Dignite ABP Modules](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/aspnetcore-mcp/index.md) hosts at `/mcp`. Other modules of the same application can add their own tools to the same server.

## What the Server Offers

* **The `site://schema` resource**, also available as the `site_get_schema` tool: the site's languages, its primary domain, its pages with their routes, the content types of each page, and the fields of each content type with their types, configuration, whether they are required, and their descriptions.
* **Tools** to manage pages, content types, fields, contents and files, and to resolve a URL path. See [Tools](tools.md).
* **Instructions** that the client receives when it connects, telling the model to read the schema first, which names to use, and the two mistakes models most often make: leaving out the slug, and inventing a language tag.

## How a Client Works with It

1. **Read the schema.** The schema is the site's own data: the names of pages, content types and fields are chosen by each tenant, and cannot be guessed. Every tool is addressed by these **names**, never by ids; only files and directories, which have no names of their own, use the ids the file tools return.
2. **Pick by description.** "Post a news item" lands on the content type whose description says it holds news, so the descriptions of content types and fields are worth writing for a model to read. They are the whole briefing it gets.
3. **Write within the schema.** Every write goes through the same application services as the admin UI and the HTTP API, so the same rules apply: values are validated against the field definitions, slugs against the page's route, languages against the site's.

Tools that only read are marked read-only, tools that can be repeated with the same result are marked idempotent, and tools that delete or migrate data are marked destructive, so a client can ask the user before calling them. The descriptions of the destructive tools also ask the model to confirm with the user first.

## Authorization

The MCP endpoint accepts the same bearer tokens as the application's HTTP API: an AI client signs in as a user of the site's tenant, and every tool runs as that user, in that tenant.

* Each tool requires the `SiteAdmin.*` [permission](../settings.md#permissions) of the application service behind it, and `tools/list` lists only the tools the user is granted.
* `site_resolve_path` requires no permission: it reads only what any visitor can see.
* The file tools check the permissions of the file's container when they are called, which are the `SiteAdmin.Contents.*` permissions; see [Files](../files.md#permissions).
* While the `Site.Enable` [feature](../settings.md#the-site-feature) is off, every tool that needs a permission is refused.

An unattended client, such as a publishing job in CI, authenticates with the client credentials flow instead. Its token carries no user, so its permissions are granted to the OAuth client itself; grant it only what that job needs.

[Connecting AI Clients](connecting-clients.md) sets up the authorization server and the clients.

## Errors

A tool that fails returns the error to the model as a tool result, not as a protocol error, with a `kind` that says what the model can do about it: `validation` with the invalid fields, `notFound`, `conflict` with the code and message of a business rule, such as `Site:040001` for a slug already in use, or `forbidden`. The model can correct the call and try again. See [Errors and Unit of Work](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/aspnetcore-mcp/errors-and-unit-of-work.md) for the shape of these results.

## Configuration

The server itself is configured by the application, with `AbpMcpServerOptions`; see [Hosting the Server](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/aspnetcore-mcp/hosting.md):

````csharp
Configure<AbpMcpServerOptions>(options =>
{
    options.ServerName = "My Sites";
});
````

Site's own options are the containers the file tools reach and the largest upload, `SiteMcpFileOptions`; see [Files](../files.md#mcp). Site raises the server's request size limit to fit one upload.

## See Also

* [Connecting AI Clients](connecting-clients.md)
* [Tools](tools.md)
* [Content Types and Fields](../content-types-and-fields.md)
