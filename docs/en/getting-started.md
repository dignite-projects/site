```json
//[doc-seo]
{
    "Description": "Install Dignite Site into an ABP application: the packages of each layer, the database, file storage, the public site, the MCP server and the Angular admin UI, then build a first page from an AI client."
}
```

# Getting Started

This guide installs Dignite Site into a layered [ABP application](https://abp.io/docs/latest/solution-templates/layered-web-application) on Entity Framework Core, then builds a first page from an AI client. The [host application in the repository](https://github.com/dignite-projects/site/tree/main/host/Dignite.Site.Host) is a complete, runnable example of everything here, in a single project.

The application needs ABP Framework 10.7 or later and the .NET 10 SDK; the Angular admin UI needs Angular 22.

## Installing the Packages

Install each package into its layer and add its module to the `[DependsOn]` attribute of that layer's module class:

| Layer | Package | Module |
|---|---|---|
| `Domain.Shared` | `Dignite.Site.Domain.Shared` | `SiteDomainSharedModule` |
| `Domain` | `Dignite.Site.Domain` | `SiteDomainModule` |
| `Application.Contracts` | `Dignite.Site.Application.Contracts` | `SiteApplicationContractsModule` |
| `Application` | `Dignite.Site.Application` | `SiteApplicationModule` |
| `EntityFrameworkCore` | `Dignite.Site.EntityFrameworkCore` | `SiteEntityFrameworkCoreModule` |
| `HttpApi` | `Dignite.Site.HttpApi` | `SiteHttpApiModule` |
| `HttpApi.Client` | `Dignite.Site.HttpApi.Client` | `SiteHttpApiClientModule` |
| The web application that serves the site | `Dignite.Site.Public.Web` | `SitePublicWebModule` |
| The application that hosts the MCP server | `Dignite.Site.Mcp` | `SiteMcpModule` |

`Dignite.Site.Application` and `Dignite.Site.HttpApi` bring both the administration and the public sides. An application that hosts only one of them installs `Dignite.Site.Admin.*` or `Dignite.Site.Public.*` instead; see [Packages](index.md#packages).

The Dignite ABP Modules packages that Site builds on (FlexFields, BLOB Storing and the MCP server) come with these packages; there is nothing to install for them.

## The Database

Site's entities live in your application's `DbContext`, so they share its unit of work and its migrations. Implement `ISiteDbContext`, replace it with your `DbContext`, and call `ConfigureSite()` in `OnModelCreating`:

````csharp
[ReplaceDbContext(typeof(ISiteDbContext))]
public class MyProjectDbContext : AbpDbContext<MyProjectDbContext>, ISiteDbContext
{
    public DbSet<Page> Pages { get; set; }
    public DbSet<ContentType> ContentTypes { get; set; }
    public DbSet<Field> Fields { get; set; }
    public DbSet<Content> Contents { get; set; }
    public DbSet<ContentFlexFieldIndex> ContentFlexFieldIndexes { get; set; }
    public DbSet<DirectoryDescriptor> DirectoryDescriptors { get; set; }
    public DbSet<FileDescriptor> FileDescriptors { get; set; }

    // ...

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ... the other modules

        builder.ConfigureSite();
    }
}
````

Then add a migration and update the database, as for any other module:

````bash
dotnet ef migrations add Added_Site
````

Site's tables are prefixed with `Site`, such as `SitePages` and `SiteContents`; change `SiteDbProperties.DbTablePrefix` before the model is built to use another prefix.

Run your application's data seeder (the `DbMigrator` of a layered solution) after the migration. It seeds the [SEO field](field-types.md#seo) into the field library of the host and of every tenant, and ABP seeds it into each tenant created later.

### MongoDB

Pages, content types, fields and contents are stored through Entity Framework Core only. `Dignite.Site.MongoDB` holds the repositories of the [file library](files.md) and nothing else, so Site needs an Entity Framework Core database.

## File Storage

Site's file library stores its bytes in two BLOB containers, `images` and `attachments`. Choose a [BLOB storage provider](https://abp.io/docs/latest/framework/infrastructure/blob-storing) for both, for example the database provider:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    foreach (var containerName in SiteFileContainerNames.All)
    {
        options.Containers.Configure(containerName, container =>
        {
            container.UseDatabase();
        });
    }
});
````

Configure only the provider: Site sets the containers' upload rules itself. A host on Linux also references `SkiaSharp.NativeAssets.Linux`. See [Files](files.md).

## The Public Site

`SitePublicWebModule` serves the site from the web application: a catch-all route that renders the pages, `robots.txt`, `sitemap.xml`, the feeds and `llms.txt`. It answers only what no other endpoint of the application claims, so the application's own pages, the API and the account pages keep working.

A page is rendered by the MVC view its `Template` names, `Default` for a new page. Add `Views/Shared/Default.cshtml` to the web application, with Site's tag helpers imported:

````cshtml
@using Dignite.Site.Public.Routing
@addTagHelper *, Dignite.Abp.FlexFields.Web
@addTagHelper *, Dignite.Site.Public.Web
@model SiteRenderViewModel
@{
    Layout = null;
}
<!DOCTYPE html>
<html lang="@Model.CultureName">
<head>
    <meta charset="utf-8" />
    <title>@(Model.HeadMetadata?.MetaTitle ?? Model.Page.DisplayName)</title>
    <site-head-metadata metadata="@Model.HeadMetadata" />
</head>
<body>
    @if (Model.Content != null)
    {
        @foreach (var field in Model.Content.Fields)
        {
            <flex-field-view field="@field" />
        }
    }
    else
    {
        <content-list page="@Model.Page" culture-name="@Model.CultureName"
                      field-filters="@Model.FieldFilters" partial-name="_ContentList" />
    }
</body>
</html>
````

The list is rendered by a partial view, `Views/Shared/_ContentList.cshtml` here. [Templates](rendering/templates.md) explains the view model, the tag helpers, the list partial and how each tenant gets templates of its own.

Set the site's primary domain, the `Site.PrimaryDomain` [setting](settings.md), before the site goes public: the canonical URLs, the sitemap and the feeds are built on it.

## The MCP Server

`SiteMcpModule` adds Site's tools to the application's MCP server, which is mapped at `/mcp`. The MCP endpoint uses the application's bearer authentication, so an AI client signs in as a user and its tools are authorized by that user's `SiteAdmin.*` permissions.

An AI client discovers where to sign in from the protected resource metadata that `AddAbpMcpAuthenticationDiscovery` publishes, and signs in with an OAuth client registered for it in your authorization server. [Connecting AI Clients](mcp/connecting-clients.md) sets up both.

## The Angular Admin UI

Install the package into the Angular application:

````bash
npm install @dignite/ng.site
````

Register its providers in `app.config.ts`, and its routes under a path of your choice:

````ts
import { provideSite } from '@dignite/ng.site/config';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...
    provideSite(),
  ],
};
````

````ts
export const APP_ROUTES: Routes = [
  // ...
  {
    path: 'site',
    loadChildren: () => import('@dignite/ng.site').then(c => c.SITE_ROUTES),
  },
];
````

Add the `SiteAdmin` API to `environment.ts`, pointing at the application that hosts the administration API, and the global stylesheets that [Admin UI](admin-ui.md#global-styles) lists to `angular.json`:

````ts
apis: {
  default: { url: 'https://localhost:44300', rootNamespace: 'MyProject' },
  SiteAdmin: { url: 'https://localhost:44300', rootNamespace: 'Dignite.Site.Admin' },
},
````

The **Site** menu appears for users who have any of the `SiteAdmin.*` permissions.

## Building the First Page

Connect an AI client to the MCP server, as [Connecting AI Clients](mcp/connecting-clients.md) describes, signed in as a user with the `SiteAdmin.*` permissions, and ask it, for example:

> Create a blog at /blog. A post has a title, a cover image and a rich-text body. Then publish a first post, "Hello, world".

The client reads the site's schema, creates the fields it needs in the field library, the `blog` page with the route `/blog/{slug}` and a content type for posts, and then the post. Open `/blog/hello-world` to see it rendered by your template, and `/sitemap.xml` to see it listed. Everything the client did can also be done in the admin UI, under **Site**.

## See Also

* [Pages and Routing](pages-and-routing.md)
* [Templates](rendering/templates.md)
* [MCP Server](mcp/index.md)
