```json
//[doc-seo]
{
    "Description": "The Angular admin UI of Dignite Site, @dignite/ng.site: installing it into an ABP Angular application, its screens and permissions, field types, and the global styles it needs."
}
```

{%{

# Admin UI

`@dignite/ng.site` is the administration UI of Site for an [ABP Angular application](https://abp.io/docs/latest/framework/ui/angular/overview). It manages everything the [MCP tools](mcp/index.md) manage, through the same administration API and the same permissions, for the people who prefer a form, or who check what an AI client did.

## Installing

````bash
npm install @dignite/ng.site
````

The package brings `@dignite/ng.flex-fields`, `@dignite/ng.flex-fields-ckeditor` with CKEditor, `ng-zorro-antd`, `@angular/cdk` and CodeMirror as its own dependencies. Its peers are the ABP 10.7 and Angular 22 packages that every ABP Angular application already has.

Register the providers in `app.config.ts`, the routes under a path of your choice, and the `SiteAdmin` API in the environment:

````ts
// app.config.ts
import { provideSite } from '@dignite/ng.site/config';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...
    provideSite(),
  ],
};
````

````ts
// app.routes.ts
{
  path: 'site',
  loadChildren: () => import('@dignite/ng.site').then(c => c.SITE_ROUTES),
},
````

````ts
// environment.ts
apis: {
  // ...
  SiteAdmin: {
    url: 'https://localhost:44300',
    rootNamespace: 'Dignite.Site.Admin',
  },
},
````

`SiteAdmin.url` is the application that hosts the administration API. The admin UI also shows the site's images from there, since file addresses are relative; see [Files](files.md#addresses).

`provideSite()` registers the menu, the field types and the CKEditor integration, and has to be in the application's configuration rather than in a lazy route: the field types must be registered before anything resolves one.

Because of that, everything `provideSite()` registers is part of the application's initial bundle, CKEditor included: it is downloaded on first paint, whether or not any content type has a rich-text field and whether or not the user opens the Site screens. Measured on a production build of an ABP admin application, registering Site added roughly 950 KB, about 170 KB gzipped. If the application enforces a bundle budget in `angular.json`, it may need to be raised. The code editor of the **Theme** screen (CodeMirror) is not part of it: it is loaded with its screen, when a user opens **Theme**.

## Languages in Setting Management

The site's languages are set on a tab of ABP's Setting Management page, not on a screen of their own. The tab belongs to a secondary entry point, so that an application without Setting Management does not need its package: add it next to `provideSettingManagementConfig()`.

````ts
// app.config.ts
import { provideSettingManagementConfig } from '@abp/ng.setting-management/config';
import { provideSiteSettingTabs } from '@dignite/ng.site/setting-management';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...
    provideSettingManagementConfig(),
    provideSiteSettingTabs(),
  ],
};
````

`@abp/ng.setting-management` is an optional peer dependency of `@dignite/ng.site`: install it when the application uses this tab. The tab is shown to a user with the `SiteAdmin.Languages` permission and can change the list with `SiteAdmin.Languages.Update`. It sets the whole ordered list; the first language is the default and its pages have no language prefix. See [Settings](settings.md).

## Screens

The **Site** menu has four entries; each is shown to users with its permission:

| Screen | Permission | What it manages |
|---|---|---|
| **Contents** | `SiteAdmin.Contents` | The contents of the site, filtered by page, content type, language, status and slug. Once a page is chosen, its fields marked `ShowInList` become columns, and its searchable fields filters. Creating or editing a content opens the editor, a form built from the content type's fields. |
| **Pages** | `SiteAdmin.Pages` | The page tree, with each page's route, template and parent. A page's **content types** are reached from here. |
| **Fields** | `SiteAdmin.Fields` | The field library: each field's type, configuration, description and group. |
| **Theme** | `SiteAdmin.Themes` | The [Scriban theme](rendering/scriban-templates.md) of the site. On the left, the files of the working copy in a tree of their folders, each marked when it is new, modified or deleted against the live release. In the middle, the open files in tabs (a file you only look at, shown in italics, gives its tab to the next one you open; a file you edit keeps its tab until you close it), in a code editor that highlights templates as HTML and static files by their extension; **Save** (or Ctrl+S) writes the file to the working copy and asks before overwriting a save someone else made in the meantime. When the file is a layout, as the check tells, a **Set as default layout** button beside it makes it the default layout, and is disabled when it already is. A file can be created, and renamed (optionally rewriting what refers to it) or deleted (after being told what uses it) from the **More actions** button at the end of its row in the tree. On the right, the result of the [check](rendering/scriban-templates.md#checking-the-theme), with a **Check** button that asks for it again; clicking a finding opens its file at its line. The check runs by itself after every change, so the button is for what happened elsewhere, such as another author or an AI client saving a file. When the file you are editing is a page's template, a **Fields** panel above the check lists the fields of that page's content types, and clicking one inserts the Scriban that prints it at the cursor: a plain value as `{{ content.fields.title }}`, a date formatted, anything richer through `render_field`. Listing them needs `SiteAdmin.Pages` and `SiteAdmin.Fields` too. **Preview** opens the site in a new tab with the saved working copy, for you only, at the home page, from where you follow the site's links; see [Previewing](rendering/scriban-templates.md#in-a-browser). **Publish** shows the check's errors, which stop it, and the files that will change, and publishes with a comment; **Releases** lists the releases, previews one, makes an older one live again or copies one back into the working copy. Editing needs `SiteAdmin.Themes.Edit`, publishing and making a release live `SiteAdmin.Themes.Publish`. |

In the **Pages** screen, the template of a page offers the theme's templates, and still accepts the name of a Razor view.

The menu is hidden while the `Site.Enable` [feature](settings.md#the-site-feature) is off, since the permissions then read as not granted.

## Field Types

`provideSite()` registers a control, a configuration editor and a view for every [field type](field-types.md):

* The FlexFields built-ins: `Text`, `Number`, `DateTime`, `Select`, `Boolean`, `Tree`, `Matrix` and `Table`.
* `CKEditor`, whose images are uploaded into Site's file library.
* Site's own `Content` (a content picker), `FileExplorer` (a file picker that browses and uploads into the file library) and `Seo` (meta title, description, share image and `noindex`).

The file picker and the SEO share image use the file library through the `SiteAdmin` API; there is no separate file package to install.

## Global Styles

Several controls are `ng-zorro-antd` components that render their panels in an overlay attached to `<body>`, and CKEditor loads its stylesheet by name, so their styles are global. An Angular library cannot add global styles to the application that uses it, so add them to `angular.json`, under `projects.<app>.architect.build.options.styles`:

````json
{ "input": "node_modules/ng-zorro-antd/tree/style/index.min.css", "inject": false, "bundleName": "ng-zorro-antd-tree" },
{ "input": "node_modules/ng-zorro-antd/tree-select/style/index.min.css", "inject": true, "bundleName": "ng-zorro-antd-tree-select" },
{ "input": "node_modules/ng-zorro-antd/select/style/index.min.css", "inject": false, "bundleName": "ng-zorro-antd-select" },
{ "input": "node_modules/ng-zorro-antd/auto-complete/style/index.min.css", "inject": true, "bundleName": "ng-zorro-antd-auto-complete" },
{ "input": "node_modules/ckeditor5/dist/ckeditor5.css", "inject": false, "bundleName": "ckeditor5" }
````

The three `inject: false` bundles are loaded at run time by their file names, `ng-zorro-antd-tree.css`, `ng-zorro-antd-select.css` and `ckeditor5.css`, so they must keep those names: injected, a production build would give them hashed names. Without these entries the build succeeds, but tree and select panels render unstyled and the CKEditor field renders empty.

## Using the Package in Your Own Screens

Besides `SITE_ROUTES`, `@dignite/ng.site` exports what its screens are built on:

* The generated proxies of the administration API, such as the page, content and field services.
* The file library's services, and `sizedFileUrl`, which builds the address of an image at a given size.
* The enums and the constants of the file containers.

## See Also

* [Getting Started](getting-started.md)
* [Field Types](field-types.md)
* [Files](files.md)

}%}
