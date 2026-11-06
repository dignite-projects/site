```json
//[doc-seo]
{
    "Description": "Write the theme of a Dignite Site in Scriban: template and static files stored per tenant, layouts chosen in front matter, partials, a working copy checked and published as a whole into releases, preview and rollback, the data and functions a template can use, the sandbox limits, and the admin API and MCP tools that manage it."
}
```

{%{

# Scriban Templates

A page can be rendered by a [Scriban](https://github.com/scriban/scriban) template instead of a Razor view. Unlike a [Razor template](razor-templates.md), which is compiled with the application and written by its developers, a Scriban template is stored in the database, belongs to one tenant, and is written by the tenant or by an AI client over the [admin API](#managing-the-theme) or the [MCP tools](#mcp-tools). It runs in a sandbox: a template can read the data it is given and call a fixed set of functions, and nothing else.

Scriban templates are available when the application uses `Dignite.Site.Public.Web`. Both engines can be used on the same site, page by page.

## The Theme

A tenant's Scriban templates, together with the CSS, JavaScript and other text files they link, form its **theme**: a set of files, each addressed by its path.

| File | Example | What it is |
|---|---|---|
| A path whose last segment has no extension | `Default`, `blog/post`, `_card`, `layouts/default` | A Scriban template. |
| A path whose last segment ends in a language | `Default.ja`, `blog/post.zh-Hant` | A Scriban template for [one language](#templates-for-one-language). |
| A path whose last segment has any other extension | `css/site.css`, `js/menu.min.js` | A [static file](#static-files), served as it is. |

No folder is reserved for templates. A new theme starts with two files, `layouts/default` and `css/site.css`, and the folders they are in are only a suggestion: arrange the files as you like. The one folder with a meaning of its own is `locales/`, which holds the theme's [texts](template-localization.md#texts-in-a-scriban-theme), one `{culture}.json` file per language. What a template is used for follows from what refers to it:

* A **page template** is a template a page's `Template` names; see [Choosing the Engine](#choosing-the-engine).
* A **partial** is a template another template `include`s; see [Partials](#partials). A leading `_` in its name, as in `_card` or `blog/_sidebar`, is a common way to tell partials apart, but it is not required.
* A **layout** is a template a page template is rendered in; see [Layouts](#layouts).

The same template can play more than one part.

A path is made of segments separated by `/`. A segment is letters, digits, `_` and `-`, starting with a letter, a digit or `_`; only the last segment may have an extension, as in `menu.min.js`. A leading slash and surrounding spaces are removed when a path is saved or looked up. The extension of a static file must be one of `css`, `js`, `mjs`, `json`, `map`, `txt`, `svg`, `xml`, `webmanifest`, `ics` and `csv`; a file with any other extension is refused, so that a template is not saved as `post.html` by mistake. The one other extension is a language, which makes the file the template's [version for that language](#templates-for-one-language).

The paths of a theme make a file tree, as a disk holds one: a path is a file or a folder, never both. A template has no extension, so its path is the name a folder would have: a template `Products/Campus` and a file `Products/Campus/Pricing` cannot both be in a theme. Name the one `Products/Campus/Index`, or the other `Products/CampusPricing`. Saving or renaming a file to such a path is refused (`Site:090016`), an [import](#exporting-and-importing) that would leave one is refused whole, and the [check](#checking-the-theme) reports one that a working copy still holds.

By default a file can be at most 256 KiB.

## Choosing the Engine

A page's `Template` is the path of a template, such as `Default` or `blog/post`. When a request is rendered, it is looked up in this order:

1. A template at that path in the **live release** of the request's tenant; see [The Working Copy and Releases](#the-working-copy-and-releases).
2. Otherwise, the **Razor view** of that name; see [Where Templates Live](razor-templates.md#where-templates-live).

There is no setting on the page: the engine follows from which of the two exists. Publishing a theme with a template at `blog/post` takes over every page whose template is `blog/post`; publishing one without it returns those pages to the Razor view of the same name. A file that has only been saved in the working copy is not rendered.

## Layouts

A layout is a complete document with `{{ body }}` where the page goes. The page template is rendered first, and its output is available to the layout as `body`; variables that the page template assigns are visible to the layout too. With a layout, the page templates write only what goes inside `<body>`; see the [example](#example).

Reading a variable that nothing has assigned is an error, as a misspelt name is, so that a typo fails the [trial render](#trial-render) instead of writing nothing. A layout that takes a variable only some of its pages assign, such as a title of their own, reads it through `this`, which gives empty for a variable that is not there:

````
<title>{{ this.title ?? page.display_name }}</title>
````

The same goes for a variable a template assigns once and then reuses, as a partial included many times may: `{{ if !this.categories }}{{ categories = find_page "blog" }}{{ end }}`.

A page template chooses its layout in **front matter**: a first line `+++`, then `layout =` and the layout's path in quotes, then a line `+++`:

````
+++
layout = "layouts/landing"
+++
<section class="hero">{{ page.display_name }}</section>
````

* Without front matter, or without `layout`, the page template uses the theme's **default layout**, a setting of the theme that names a template. A new theme's default layout is `layouts/default`; it can name any template, or be cleared.
* `layout = "none"` renders the page template alone, as a complete document.
* Front matter is not part of the output. It may set nothing but `layout`, to a path in quotes; anything else is an [error](#checking-the-theme). Front matter is read only in a page template: a partial's is ignored, and a layout cannot have one.

A page must call `site_head`, which writes the head metadata of the page: the description, the canonical URL, the robots tag, Open Graph (`og:title`, `og:site_name` and `og:locale` included), the Twitter card, the hreflang alternates, and the structured data the template gives it; see [The Head](#the-head). It does not write the `<title>` element, the charset or the viewport, which stay the layout's own: write the title as `<title>{{ head?.meta_title ?? page.display_name }}</title>`. Without `site_head`, the page would lose its [SEO](../seo/index.md) tags. So the rule is enforced twice:

* The [check](#checking-the-theme) refuses a layout, or a page template rendered without a layout, that calls `site_head` nowhere, neither itself nor in a template it includes. A theme with such an error cannot be published.
* Rendering a page whose render never executed `site_head` fails with the error `Site:090005`, instead of serving a page without the tags.

`site_head` writes the same tags as the `<site-head-metadata>` tag helper of a Razor view. On the second page of a paged list and beyond, the canonical URL and the alternates carry the page number; see [Paging](#paging).

## The Head

`site_head` takes either two optional arguments, a title and a description, or one object of options:

````
{{ site_head "Archive" }}
{{ site_head { image: content.fields.cover, type: "article" } }}
````

| Option | Description |
|---|---|
| `title` | Overrides `og:title`, which is the content's title otherwise. |
| `description` | Used only when the content has no description of its own. |
| `image` | A share image for the page, used when the content's SEO field has none, in place of the site's default share image: an address, or a file field's value, of which the first file is taken. An image of the file library is cropped to 1200 x 630. |
| `type` | `og:type`: `website`, the default, or `article`, which also writes the content's `article:published_time` and `article:modified_time`. |
| `schema` | Structured data: a schema.org node, or a list of them; see below. |

An option that is not in the table, or a `type` that is neither, fails the render.

The share image is the first of: the image of the content's SEO field, the template's `image`, and the site's default share image (`Site.Seo.DefaultShareImage`, see [Settings](../settings.md)). `og:image:alt` is not written: a template that wants it writes its own tag after `site_head`.

### Structured Data

The template decides which schema.org types a page carries and what goes in them, and gives them to `site_head` as `schema`; Site writes them in a `<script type="application/ld+json">` and makes sure they are written correctly:

* Several nodes become one `@graph` under one `@context`; a single node gets the `@context` itself.
* A property with nothing in it, such as a field that is empty on this content, is left out, at any depth, so a template need not ask first.
* An address written `~/...` is put in the page's language, as [`url`](#functions) does, and made absolute on the site's primary domain; so is an address of the file library (`/api/site-public/files/...`). Any other text is written as it is.
* `<`, `>` and `&` are escaped, so a title that holds `</script>` cannot end the script.

Each node must be an object with an `@type`; anything else fails the render. A key such as `@type` is written in quotes:

````
{{ crumbs = [] }}
{{ for step in breadcrumb }}
  {{ crumbs = array.add crumbs { "@type": "ListItem", position: for.index + 1, name: step.name, item: step.url } }}
{{ end }}
{{ site_head { schema: [
  { "@type": "Organization", "@id": "~/#org", name: "Example", url: "~/", logo: "~/logo.png" },
  { "@type": "BreadcrumbList", itemListElement: crumbs }
] } }}
````

`breadcrumb` is the way from the home page to the page being rendered: each step has `name`, `path` (for a link on the page), `url` (absolute, for structured data), `page_name`, `slug` (on a content's step) and `is_current`. `name` is the page's title, which can be long for a breadcrumb; to label the steps with the theme's own texts, look them up by `page_name`, keeping `name` as the fallback:

````
{{ for step in breadcrumb }}
  <a href="{{ step.path }}">{{ step.slug ? step.name : (t ("Breadcrumb:" + step.page_name) step.name) }}</a>
{{ end }}
````

See [Breadcrumbs](../seo/index.md#breadcrumbs).

## Templates for One Language

Most pages need a single template for every language: the text comes from the content and from [`t`](template-localization.md#texts-in-a-scriban-theme), and the structure is the same. When a language needs a different structure, give the template a **version for that language**: a file with the template's path and the language as its extension.

````
Default             the template, for every language
Default.ja          the Japanese version of Default
blog/post.zh-Hant   the Traditional Chinese version of blog/post
````

* A version is looked up for the page's language, then for each of its parent languages: on a page in `zh-Hant-TW`, `Default.zh-Hant-TW`, then `Default.zh-Hant`, then `Default.zh`. The first that exists is rendered. If none does, the template itself is, so a language without a version is never left without a page. The site's default language is not looked for: its version is the template itself.
* The page template, its layout and every template it includes are looked up this way, each for its own version. A page in Japanese is rendered by `Default.ja` if there is one, in `layouts/default.ja` if there is one, and an `{{ include "_card" }}` in either includes `_card.ja` if there is one.
* A version replaces the template; it does not add to it. It has its own front matter, so it can choose a layout of its own, or `layout = "none"`.
* The extension has to be a language the runtime knows, spelled the way it spells it: `ja`, `zh-Hant`, `en-GB`. A file named `post.tpl` or `Default.zh-hans` is refused when it is saved, like any extension no static file may have.
* A version needs its template. A page's `Template` and the default layout name the template, a path without a dot, never a version, and the [check](#checking-the-theme) reports a version whose template is missing. Naming a version in full, as in `{{ include "_card.ja" }}`, includes exactly that file in every language.
* Renaming a template renames its versions with it.

A [preview](#previewing) reports the template it rendered as `pageTemplate`, which is the version when there was one.

## The Working Copy and Releases

A tenant edits its theme in a **working copy**, and the site renders a **release**: a frozen copy of the whole theme, files and default layout together.

* **Saving** a file changes the working copy only, in place: there is no version per save, and the site does not change. A template with a syntax error can be saved; the error is reported with the file, and the theme cannot be published until it is fixed.
* **Publishing** checks the working copy and, when the check finds no error, freezes it into the next numbered release and makes that release live, in one step. Every file changes at once, so the site never renders a mix of old and new files.
* **Rolling back** makes an older release live again. The working copy is not touched.
* **Restoring** a release copies its files and default layout back into the working copy, replacing it, to carry on from there. Nothing goes live.
A release keeps every file as it was published, and the list of releases tells, for each one, its number, its comment, when it was published and by whom, and how many files it changed. Releases are never deleted.

A publish is refused when:

* the check finds an error (`Site:090002`);
* the working copy changed since it was checked (`Site:090003`): publishing takes the fingerprint the check returned, so that what goes live is what was checked;
* nothing changed since the live release (`Site:090010`).

Renaming a file can rewrite, in the same step, every `include`, `asset_url` and front matter `layout` that names it by its path in quotes, and the default layout setting. The pages whose `Template` names the old path are not changed, and are listed. Deleting a file removes it from the working copy; the live release keeps it until the next publish.

## Checking the Theme

The check reads the whole working copy, and the pages' templates, and lists what is wrong. Only literal paths in quotes are followed: a path computed when the template runs cannot be known before it runs.

These are errors, and a theme with any of them cannot be published:

* A template has a syntax error.
* Front matter sets something other than `layout = "..."`, or a layout has front matter.
* The default layout names no template of the theme.
* An `include`, an `asset_url` or a `layout` names a file the theme does not have, or the wrong kind of file: `include` and `layout` take a template, `asset_url` a static file.
* Templates include each other in a circle.
* An `include` leaves out a named argument that the template reads as a local variable: a partial that writes `$title` fails the render wherever it is included without `title:`. A partial's own `$x = ...`, `for $item in ...` and `capture $x` count as given. Pass `title: null` when the argument is optional.
* A layout, or a page template rendered without a layout, never calls `site_head`, not even in what it includes.
* A file in `locales/` is not a valid file of the theme's [texts](template-localization.md#texts-in-a-scriban-theme), or two files hold the texts of the same language.
* A [version for one language](#templates-for-one-language) has no template to be the version of.
* A file's path is also the folder of other files, as `Products/Campus` is of `Products/Campus/Pricing`; see [The Theme](#the-theme).

The versions of a template are checked as the template is: the versions of a layout must write `body` and call `site_head`, those of a page template have their front matter and their layout checked, and those of an included template have their `include`s followed.

These are warnings:

* A page's `Template` is not a template of the theme, so a Razor view renders it.
* A layout never writes `{{ body }}`.
* A layout is also a page template, or is included.
* Nothing uses a file: no page, template or setting refers to it. The files in `locales/` are never reported, since `t` reads them by language, and a version for one language is never reported apart from its template.

Each finding names its file and, where it has one, the line and the column, with a message in the user's language.

### Trial Render

Reading the templates cannot find everything: a variable that does not exist, or a partial that fails on one kind of content, passes the reading and breaks the page when it renders. So once the reading finds no error, the check also **renders a sample of the site's pages with the working copy**, and a template that fails to render is an error, reported at the template, line and column of the failure, with the address that exposed it. Publishing renders the sample again and is refused when it fails, so the theme that goes live is one that rendered.

The sample is what a visitor would open, for each page rendered by a template of the theme: the page's own address in each language the site serves, one published content for each content type and language under the page, and the second page of the list (`?page=2`) in each language with more than one content, since a list, a detail and a later page run different parts of a template. Drafts are not rendered. A page the theme has no template for is not rendered, as a Razor view renders it.

The sample is bounded, at 100 addresses and 30 seconds. Within the bound, each template, and each of its [versions for a language](#templates-for-one-language), is rendered once as a list, once as a detail of each content type and once as a later page before any of them is rendered a second time, so the addresses a large site leaves out repeat what was rendered. When the run does not render the whole sample, a warning says why:

* `ThemeIssue:TrialRenderIncomplete`: the sample has more addresses than the bound.
* `ThemeIssue:TrialRenderOutOfTime`: the 30 seconds ran out.
* `ThemeIssue:TrialRenderStopped`: an address failed for a reason that is not the theme's, such as a service the render needs, and the run stopped there. The warning names the address; the application's log has the failure.

Only the theme's failures are findings. An address that leads nowhere, or a failure of the site itself while rendering, is not.

The pages are rendered by `Dignite.Site.Public.Web`, in the same application as the administration API or in the application that serves the site; see [When the Site Runs in Another Application](#when-the-site-runs-in-another-application). An application that has neither renders nothing: the check says so in a warning (`ThemeIssue:TrialRenderUnavailable`), and publishing goes ahead without the trial render, so a template that fails only when it renders is not found before it goes live.

The rendered pages are also read for links to an id that nothing on the page has: a `<use href="#icon">` whose symbol is not in the page shows nothing and fails nothing, and an anchor to a section that does not exist goes nowhere. Each is a warning, once per id, with the first address that has it (`ThemeIssue:FragmentReferenceNotFound`); a warning does not stop a publish. A reference to another file (`href="sprite.svg#icon"`), an empty `#` and `#top` are not reported.

## Previewing

The site can be seen with the working copy, or with any release, without publishing anything: in a browser, where you click through the site as a visitor would, or as the HTML of one page, over the API and MCP. Either way a preview renders the saved files, and the data, the functions and the limits are the live site's.

### In a Browser

**Preview** in the Theme screen opens the site's home page in a new tab with the working copy, and you follow the site's links from there as a visitor would. **Releases** has a **Preview** for each release, to look at one before making it live. The link is signed for you and valid for ten minutes. Opening it sets a cookie in that browser, and for the next two hours every page and static file of the site you open there renders with that theme. Visitors keep seeing the live release.

The working copy is read again on every page view: save in the editor, then reload the preview tab to see the change. Unsaved changes are not in the preview.

A page rendered by a Scriban template carries a banner at the top that says what is previewed, with an **Exit preview** link that removes the cookie and reloads the page as it is live; a page still rendered by a Razor view has no banner. `/site-preview/exit` ends a preview too. Every response of a preview is sent with `Cache-Control: no-store` and `X-Robots-Tag: noindex`, so no cache keeps it and no search engine indexes it. A template that fails shows a page with the error, the template, and for a run-time error the line and the column, instead of the site's error page.

The link is issued by the administration API, `GET /preview-link` (see [Managing the Theme](#managing-the-theme)), and points at the tenant's site address, the `Site.PrimaryDomain` setting that canonical links and the sitemap use, so it works when every tenant has a domain of its own. Without that setting the link fails with `Site:050001`. The site reads the link with the application's Data Protection keys: when several instances serve the site, they must share one key ring, as they already must for authentication cookies.

### As HTML

The path is resolved as a request would be: `/about` or `/ja/blog/hello`, with a language prefix if the site uses them, and a leading slash is not required. It may carry the page of a paged list, `/ja/blog?page=2`; see [Paging](#paging). The page or content at that path is rendered through its template and layout as the given theme has them.

A preview renders saved files, so save first. It fails with one of these errors:

* `Site:090006` when no page answers the path.
* `Site:090007` when there is nothing to render with: a preview renders the page the way the site does, with `Dignite.Site.Public.Web`, in the same application as the administration API or in the one [named for it](#when-the-site-runs-in-another-application).
* `Site:090009` when the page's `Template` is not a template of the previewed theme, so a Razor view renders it.

A render that fails, through a run-time error, a template that does not parse or a limit, fails the preview with `Site:090008`, naming the template and, for a run-time error, the line and the column. A render that never executes `site_head` fails with `Site:090005`, as the live page does.

The admin API answers these errors with a status of their own, rather than ABP's `403` for a business error, which would read as a missing permission: `404` for `Site:090006`; `422` for `Site:090005`, `Site:090008` and `Site:090009`; and `501` for `Site:090007`.

You can preview over the [admin API](#managing-the-theme), which returns the HTML with the page, the template and the layout it was rendered with, and over MCP, with `site_preview`, so that an AI client can check its own changes before it publishes; see [MCP Tools](#mcp-tools). An AI client that has a browser also looks at the site as a visitor would: `site_get_preview_link` returns the [browser preview](#in-a-browser) link for a path, which it opens and takes a screenshot of, where `site_preview` gives it the markup and the head tags a screenshot does not show.

### When the Site Runs in Another Application

When the administration API and the site are deployed as two applications, the administration API has no renderer and the site has no access to the working copy. The two call each other for a preview, so the three ways of previewing (in a browser, as HTML, and the trial render) work as they do in one application:

* The administration API sends the page and the theme to the site's `POST /site-preview/render`, which renders it with the site's own views and services.
* The site reads the theme a preview cookie names from the administration side's application, through an [integration service](https://abp.io/docs/latest/framework/api-development/integration-services) under `/integration-api/site-public/`.

Each call carries a token that lives one minute, signed with the Data Protection keys. A call without one is answered with nothing, and a browser preview's token is not accepted. To set it up:

1. Give both applications one Data Protection key ring: the same application name and the same key storage, such as Redis.
2. Set each tenant's `Site.PrimaryDomain`, where the preview links go.
3. In the administration API's application, name the site's application. An internal address will do:

   ````json
   "RemoteServices": {
     "SitePublicWeb": { "BaseUrl": "http://site-public-web" }
   }
   ````

4. In the site's application, name the application that holds the data, which serves the administration API. An API gateway does not forward `/integration-api/`, so give its own address:

   ````json
   "RemoteServices": {
     "SitePublicIntegration": { "BaseUrl": "http://site-service" }
   }
   ````

5. In the application that holds the data, expose the integration services, and keep `/integration-api/` off the public network:

   ````csharp
   Configure<AbpAspNetCoreMvcOptions>(options => options.ExposeIntegrationServices = true);
   ````

Without the third step, a preview as HTML fails with `Site:090007` and the trial render warns that it rendered nothing. Without the fourth or the fifth, a page opened through a preview link renders with the live release, under a banner that says so.

## What a Template Sees

A template is given data, and only data: the page and the content being rendered, and the language. The names are in `snake_case`, and every date and time is a real date, so `date` functions and members such as `content.publish_time.year` work.

| Variable | Description |
|---|---|
| `page` | The resolved page: `id`, `name`, `display_name`, `route`, `template`, `is_home_page`, `parent_id`, `is_active` and the audit fields. |
| `content` | The resolved content, or empty when the request names the page itself, such as a list. It has `id`, `page_id`, `content_type_id`, `culture_name`, `slug`, `publish_time`, `status` (a lower-case text such as `"published"`), `url`, `fields`, `field_list` and the audit fields. |
| `content.fields` | The values of the content, keyed by the field names exactly as the site defined them: `content.fields.title`. A value has its JSON shape, and the members of an object inside a value are in `camelCase`, as over HTTP. |
| `content_type` | The content type of `content`, or empty when the request names the page itself. It has `id`, `page_id`, `name`, `display_name`, `description` and `fields`, each of which has `field_id`, `required`, `searchable`, `show_in_list`, `display_name` and `order`; the definitions of the fields come from [`field_definitions`](#pages-and-field-definitions). |
| `field_list` | The fields of `content`, in the content type's order, without the SEO field. Each has `name`, `display_name`, `field_type_name`, `required` and `value`. An empty list when there is no content; the same list as `content.field_list`. |
| `culture` | The language of the request, such as `en` or `zh-Hans`. |
| `default_culture` | The site's default language. |
| `languages` | The site's languages, for a language switcher. Each has `culture_name`; `native_name`, the language's name in itself, such as `日本語` or `Français`, with its first letter capitalized; `url`; `is_current`; and `is_translation`, whether the current content exists in that language. |
| `filter_values`, `field_filters` | What the path gave the route's placeholders, with the keys as the route wrote them: `filter_values["publishTime:yyyy"]`. See [The View Model](razor-templates.md#the-view-model). |
| `published_after`, `published_before` | The period that `publishTime` placeholders named, as dates, or empty. |
| `page_no` | The page of a paged list that the request asks for with `?page=2`, or 1. See [Paging](#paging). |
| `query` | The query string of the request: each parameter's first value, by its name in lower case, such as `query.service` on `/start?service=design`; empty for a parameter the request does not have. It is the visitor's text: it is encoded when written, like any value, and is best compared with the values the template expects before it chooses anything. A [preview](#previewing) has the query string of the previewed path. |
| `head` | The head metadata of the request, or empty: `meta_title`, `base_meta_title`, `meta_description`, `canonical_url`, `robots_content`, `og_image_url`, `is_default_og_image`, `og_image_width`, `og_image_height`, `og_type`, `twitter_card_type`, `site_name`, `locale`, `locale_alternates`, `base_url`, `hreflang_alternates` (each with `culture_name`, `url` and `path`), `x_default_url` and `breadcrumb`. |
| `breadcrumb` | The way from the home page to the request: each step has `name`, `path`, `url`, `page_name`, `slug` and `is_current`. See [The Head](#the-head). |
| `body` | In a layout only: the rendered page template. |

### Output Encoding

Every value a template writes is HTML-encoded, as in a Razor view: `{{ content.fields.title }}` is safe even when the title holds markup. Three outputs are written as they are:

* the output of `site_head`,
* the output of `render_field`, which the field type has already encoded or sanitized for its kind of value,
* `body`, in a layout.

`html_raw` marks a value as trusted HTML, so that it is written as it is. It bypasses the field type's own sanitizing, so use `render_field` for rich text and keep `html_raw` for HTML that you control.

## Functions

Besides Scriban's own `string`, `array`, `math`, `date`, `regex` and the like, a template can call these functions. Their names cannot be reassigned.

| Function | Description |
|---|---|
| `site_head [title] [description]`, `site_head options` | Writes the head tags and the structured data; see [The Head](#the-head). |
| `t key [fallback] [arguments...]` | A localized text, in the language of the page; `fallback` when the key has no text. Arguments fill the text's placeholders, as `L["key", arguments]` does in a Razor view: `{0}`, `{1:N0}`, `{0:yyyy-MM}`, numbers and dates formatted in the language of the page, at most 10. Pass `null` as the fallback to give arguments without one: `t "Pager:Page" null page_no`. A text with a placeholder no argument fills fails the render. It can be piped: `{{ "Key" | t }}`. The texts are the theme's own, in `locales/{culture}.json`, and nothing else. See [Localizing Template Texts](template-localization.md). |
| `url path [culture]` | The address of a path in a language. Only a path that starts with `~/` gets the language prefix and the application's path base; anything else is returned unchanged. The language of the request by default. |
| `page_url page_no` | The address of a page of the list the current page is, for a pager; see [Paging](#paging). |
| `file_url url width [height]` | The address of a stored image resized to the given size, from a file field's value. |
| `contents [options]` | Lists published contents; see below. |
| `content_counts options` | How many published contents carry each value of a field; see [Counting Contents by a Field](#counting-contents-by-a-field). |
| `adjacent_contents [options]` | The contents published just before and just after `content`; see [Older and Newer Contents](#older-and-newer-contents). |
| `render_field name [show_in_list]` | Renders a field of `content` with the view of its field type, as `<flex-field-view>` does. With `show_in_list` true it renders the value alone, without the field's label, as a list shows it. |
| `find_page name` | A page by its name, with its content types; see [Pages and Field Definitions](#pages-and-field-definitions). |
| `field_definitions fields` | The definitions of fields, with their configuration, keyed by field name; see [Pages and Field Definitions](#pages-and-field-definitions). |
| `html_raw value` | Marks a value as trusted HTML; see [Output Encoding](#output-encoding). |
| `include path [arguments]` | Renders another template in place; see [Partials](#partials). |
| `asset_url path` | The address of a [static file](#static-files) of the theme, with the hash of its content, such as `/site-assets/css/site.css?v=7b58...6970`. |
| `app_file_url path` | The address of a file the application serves from its web root, not the theme, such as `app_file_url "/libs/abp/core/abp.js"`: under the application's path base, with a version from the file's content, as `asp-append-version` writes it in a Razor view, so a browser that keeps it for long never keeps an old one. The path starts with `/`. |
| `text_width text` | How many columns a text takes in a search result: two for a Chinese, Japanese or Korean character, one for any other. It is the measure of the limits of the [SEO field](../seo/index.md), for a template that decides whether a title still fits with the site's name after it. |

`contents` takes an object with these keys, all optional, and returns the page of contents with `items`, `total_count`, `max_result_count` and `current_page`:

| Key | Description |
|---|---|
| `page` | The name of the page to list. Omit it to list the current page, with the current route's `field_filters`, `published_after` and `published_before` applied. With a `page`, none of the route's filters apply. |
| `content_type` | The name of a content type of that page. |
| `max` | The page size, 20 by default and at most 100. |
| `page_no` | The 1-based page number, 1 by default. Pass the request's, `page_no: page_no`, to follow the address; see [Paging](#paging). A page past the end gives an empty list. |
| `filter` | A text filter over the slug. |
| `field_filters` | An object of field names and values: only the contents whose field holds the value, such as `{ category: "news", featured: true }`. The field must be one that a content type of the page marks **Searchable**, as for a route placeholder; a filter on any other is ignored. On the current page it adds to the route's filters, and replaces the route's value of a field both name. |
| `published_after`, `published_before` | Dates that bound `publish_time`, both included: a date, or a text such as `"2026-01-01"`. On the current page they replace the route's. |

A key that is not in the table is an error, and a `page` that does not exist gives an empty list. Contents are listed newest first, and only published ones. Each item has the shape of `content`, except that its `field_list` holds only the fields marked Show In List.

To list another page with the current route's filters, as an archive page that lists the posts of its child page does, pass them on: `contents { page: "posts", field_filters: field_filters, published_after: published_after, published_before: published_before }`.

````
{{ news = contents { page: "news", max: 5 } }}
<ul>
{{ for item in news.items }}
    <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
{{ end }}
</ul>
````

### Paging

A visitor turns the pages of a list with the `page` query parameter: `/blog` is the first page and `/blog?page=2` the second. The template never reads the address itself; the request's page number is the variable `page_no`, 1 when the address has none, or has something that is not a page number. A list follows it when the template passes it on, `contents { page_no: page_no }`, and stays on its first page otherwise, as a "latest posts" block beside the list should.

`page_url` writes the address of another page of the same list, the current path with the page number: `page_url 1` is `/blog`, `page_url 3` is `/blog?page=3`, in the language of the request and under the application's path base. `site_head` needs nothing passed: on the second page and beyond, it writes the canonical URL and the alternates with the page number, so that each page of the list is indexed as its own.

````
{{ list = contents { max: 10, page_no: page_no } }}
<ul>
{{ for item in list.items }}
    <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
{{ end }}
</ul>
{{ if page_no > 1 }}<a href="{{ page_url page_no - 1 }}">Newer</a>{{ end }}
{{ if page_no * list.max_result_count < list.total_count }}<a href="{{ page_url page_no + 1 }}">Older</a>{{ end }}
````

The name of the query parameter is the host's `SiteScribanOptions.PageQueryKey`, `page` by default; `page_no`, `page_url` and `site_head` all follow it.

### Counting Contents by a Field

`content_counts` counts the published contents of a page by the value of one of their fields, such as the number of news items per category. It takes an object with these keys and returns a list of groups, most numerous first, each with `value` and `count`:

| Key | Description |
|---|---|
| `field` | Required. The name of the field to group by. |
| `page` | The name of the page to count. Omit it to count the current page, with the current route's `field_filters`, `published_after` and `published_before` applied. With a `page`, none of the route's filters apply, and all of that page's published contents are counted, unless the keys below filter them. |
| `content_type` | The name of a content type of that page. Omit it to count the contents of all of the page's content types. |
| `filter` | A text filter over the slug. |
| `field_filters`, `published_after`, `published_before` | Filters, as `contents` takes them. |
| `max` | The most groups to return, 100 by default and at most 1000. |

It counts the contents that `contents` lists with the same keys, so a count and the list beside it always speak of the same contents. A layout is shared by every page, so a `content_counts` in a layout without a `page` counts whichever page is being rendered, and gives nothing on a page that lacks the field. Name the `page` there. Three things differ from `contents`:

* The field has to be one that a content type of the page marks **Searchable**, as for a route placeholder. For any other field, an unknown one, or a field of a type that cannot be indexed, such as rich text, the list is empty.
* On the current page, a route filter on `field` itself is left out. On a page routed `/blog/{category}`, the counts are those of every category, not only of the one being viewed.
* A content with several values in the field, such as a multiple-choice `Select`, is counted under each of them, so the counts can add up to more than the number of contents. A content with no value is in no group.

`value` is the value as it is stored: the `Value` of a `Select` option, not its `Text`. The text comes from the definition of the field. This extends the menu of the categories in [Pages and Field Definitions](#pages-and-field-definitions) with the number of contents of each:

````
{{ blog = find_page "blog" }}
{{ if blog }}
    {{ fields = field_definitions blog.content_types[0].fields }}
    {{ if fields.category }}
        {{ counts = {} }}
        {{ groups = content_counts { field: "category", page: "blog" } }}
        {{ for group in groups }}
            {{ counts[group.value] = group.count }}
        {{ end }}
        {{ for option in fields.category.configuration["Select.Options"] }}
            <a href="/blog/{{ option.Value }}">{{ option.Text }} ({{ counts[option.Value] ?? 0 }})</a>
        {{ end }}
    {{ end }}
{{ end }}
````

### Older and Newer Contents

`adjacent_contents` returns the contents either side of `content`, among the published contents of its page in its language, by publish time, for "older" and "newer" links: `previous`, the next older one, and `next`, the next newer one, each shaped like an item of `contents`, or empty when there is none that way. Both are empty on a page that renders no content. It takes an object with one optional key, `content_type`, which keeps both to the contents of that content type of the page. It is what `IContentPublicAppService.GetAdjacentAsync` gives a Razor view, and it finds them however many contents the page has, which looking for `content` among the items of `contents`, at most 100 at a time, would not.

````
{{ around = adjacent_contents }}
<nav>
    {{ if around.previous }}<a href="{{ around.previous.url }}">{{ around.previous.fields.title }}</a>{{ end }}
    {{ if around.next }}<a href="{{ around.next.url }}">{{ around.next.fields.title }}</a>{{ end }}
</nav>
````

### Pages and Field Definitions

`find_page` returns a page by its name with its content types, the current page as well as any other. It has the members of `page` and `content_types`, each shaped like `content_type`. The `fields` of a content type are its uses of fields (`field_id`, `required`, `searchable`, `show_in_list`, `display_name` and `order`); the definition of a field comes from `field_definitions`. It returns nothing when no active page has the name, and a page is looked up once per render, however often a template asks for it.

`field_definitions` takes the `fields` of a content type, a list of field ids or a single one, and returns an object keyed by field name. Each field has `id`, `name`, `display_name`, `description`, `field_type_name` and `configuration`. A call takes at most 100 fields. Only fields that a content type of the render names are resolved: those of `content_type`, and those of the pages `find_page` returned. Any other id, and an id that no field has, is left out. A definition is fetched once per render.

`configuration` is the configuration of the field type, with its keys and members as they are stored. A key starts with the name of the field type and a dot, such as `Select.Options` and `Select.Multiple`, so it is read with brackets, and the members of an object inside are capitalized, such as the `Text` and `Value` of an option. Each field type's configuration is described in [Field Types](../field-types.md).

For example, a menu of the categories of the blog, from the options of the `Select` field `category`, which is in the first content type of the page `blog`:

````
{{ blog = find_page "blog" }}
{{ if blog }}
    {{ fields = field_definitions blog.content_types[0].fields }}
    {{ if fields.category }}
        {{ for option in fields.category.configuration["Select.Options"] }}
            <a href="/blog/{{ option.Value }}">{{ option.Text }}</a>
        {{ end }}
    {{ end }}
{{ end }}
````

Reading a member of a field that is not there, as `fields.category.configuration` is when the content type has no field `category`, fails the render, so the example asks first.

The definitions of the fields of the content being rendered need no `find_page`: `field_definitions content_type.fields`.

## Partials

A partial is a template that holds a fragment of markup, so that a page template or a layout can pull it in instead of repeating it. Any template of the theme can be included, by its full path:

````
{{ include "blog/_post-card" }}
````

The rules of `include`:

* The path is the template's full path from the theme's root, whatever folder the including template is in.
* Only a template of the theme being rendered is found: the live release for the site, the previewed working copy or release for a preview. A template that is not there fails the render with an error that names it.
* A static file cannot be included.

A partial renders in the context of the template that includes it. It sees the same data and functions, and the variables that the including template has assigned. Arguments after the path pass values explicitly:

* Positional arguments are available in the partial as `$1`, `$2` and so on.
* Named arguments become local variables of the partial, read with the `$` prefix that Scriban gives a local: `{{ include "_card" title: "Hi" }}` gives the partial a `$title`. A partial reads only the locals it was passed or assigned itself, not those of the template that includes it, and a local it reads but was not passed fails the render; the [check](#checking-the-theme) reports the `include` that leaves it out.

A partial's output is encoded the way any template's is: every value it writes is HTML-encoded once, and the result of `include` is placed into the including template as it is, not encoded a second time. A partial can include other partials. `include_join` is not available.

The theme is read together with the page, so a page costs no query per partial it includes, and the set of templates a render sees is fixed when the render starts.

For example, a `blog/_post-card` that writes one item of a list:

````
<article>
    <h2><a href="{{ item.url }}">{{ item.fields.title }}</a></h2>
    <p>{{ item.publish_time | date.to_string "%Y-%m-%d" }}</p>
</article>
````

A page template that lists the contents of the current page and includes the partial for each one. `item` is assigned by the loop, so the partial sees it:

````
{{ list = contents { max: 10 } }}
{{ for item in list.items }}
    {{ include "blog/_post-card" }}
{{ end }}
````

The same partial can take the item as an argument instead, `{{ include "blog/_post-card" item }}`, and read it as `$1`. A named argument, `{{ include "blog/_post-card" post: item }}`, is read as `$post`.

## Static Files

A static file is CSS, JavaScript or another text file of the theme, a file whose name has an extension, such as `css/site.css`. It is published with the rest of the theme, with two differences from a template:

* It is stored as it is and is **not** parsed as Scriban, so `{{` in a stylesheet or a script is just text.
* It is served, unrendered, by the application at `GET /site-assets/{path}`, where `{path}` is its full path: `css/site.css` is served at `/site-assets/css/site.css`.

Only the live release's files are served. A file the live release does not have answers 404; so does a template's path. The theme's texts in `locales/` are static files too, and are served the same way, so keep nothing private in them.

The response carries these headers:

| Header | Value |
|---|---|
| `Content-Type` | From the extension, with `charset=utf-8`: `css` is `text/css`; `js` and `mjs` are `text/javascript`; `json` and `map` are `application/json`; `svg` is `image/svg+xml`; `xml` is `application/xml`; `webmanifest` is `application/manifest+json`; `txt` is `text/plain`; `csv` is `text/csv`; `ics` is `text/calendar`. |
| `X-Content-Type-Options` | `nosniff`. |
| `ETag` | The hash of the content. A request whose `If-None-Match` matches it is answered 304. |
| `Cache-Control` | `public, max-age=31536000, immutable` when the `v` query of the URL is the hash of the content served; otherwise `public, no-cache`, which is served but revalidated each time. |
| `Set-Cookie` | None: a response a shared cache may keep carries no cookie, even though the application's tenant and language handling would set one on a page. |

`asset_url` writes the address that earns the long cache. `asset_url "css/site.css"` returns `/site-assets/css/site.css?v=` followed by the SHA-256 of the file's content, under the application's path base when it has one. A release that changes the file changes the address, which busts the browser and CDN caches, while a file that did not change keeps its address, and its cached copy, from one release to the next. The path must be a static file of the theme, or the render fails with an error that names it.

For example, a stylesheet at `css/site.css`:

````
body { font-family: system-ui, sans-serif; margin: 0; }
nav a { margin-right: 0.5rem; }
````

referenced from a layout:

````
<head>
    <meta charset="utf-8" />
    {{ site_head }}
    <link rel="stylesheet" href="{{ asset_url "css/site.css" }}" />
</head>
````

The same goes for a script: `<script src="{{ asset_url "js/site.js" }}" defer></script>`.

Binary files, such as images and fonts, are not theme files. They stay in the [file library](../files.md) and are addressed with `file_url`.

## Limits

A template runs in a sandbox, so that a template written by a tenant or by an AI cannot take the application down or read what it should not.

* A template reaches only the variables and functions above. It cannot call into the application or read files. It can `include` only the templates of the theme it is rendered with, which belongs to its own tenant.
* Partials nest at most 10 deep, and a partial that includes itself, directly or through others, fails with an error that lists the chain. A host can change the depth with `SiteScribanOptions.MaxIncludeDepth`.
* The loops of one template run at most 10,000 iterations per outermost loop, nested loops counted together, and nested calls go at most 100 deep.
* A regular expression match is stopped after 100 ms.
* One template writes at most 2 Mi characters (2,097,152).
* Rendering one page, layout included, must finish within 3 seconds.

The loop, recursion, output and time limits apply to the whole render, partials included. A template that exceeds a limit, or fails at run time, fails the request with an error that names the template and the line and column; a timeout is reported as a timeout, naming the template. A host can change the limits with `SiteScribanOptions`.

## Managing the Theme

The **Theme** screen of the [admin UI](../admin-ui.md#screens) edits, checks and publishes the theme. The admin API manages it under `api/site-admin/theme`. A file's path travels as the `path` query parameter, since it contains slashes. The theme is created on first use, with its two starting files; creating it publishes nothing.

| Request | Permission | Description |
|---|---|---|
| `GET /` | `SiteAdmin.Themes` | The theme: its default layout, the live release's id and number, and a concurrency stamp. |
| `PUT /` | `SiteAdmin.Themes.Edit` | Sets the default layout, the path of a layout of the working copy or empty for none. A template that writes no `body`, never calls `site_head` (itself or through what it includes) or has front matter is refused as not a layout; a template that does not parse is let through, for the check to report. |
| `GET /files` | `SiteAdmin.Themes` | The files of the working copy, each with its status against the live release: unchanged, modified or added; a file the live release has and the working copy no longer does is listed as deleted. |
| `GET /files/by-path?path=...` | `SiteAdmin.Themes` | One file with its body, its concurrency stamp and its syntax errors. |
| `PUT /files` | `SiteAdmin.Themes.Edit` | Creates or overwrites a file, with `path`, `body` and, to detect a change by someone else, the `concurrencyStamp` it was read with. Returns the file with its syntax errors. |
| `POST /files/rename` | `SiteAdmin.Themes.Edit` | Moves a file, with `path`, `newPath` and `updateReferences`, true by default; see [The Working Copy and Releases](#the-working-copy-and-releases). |
| `DELETE /files/by-path?path=...` | `SiteAdmin.Themes.Edit` | Removes a file from the working copy. |
| `GET /files/usages?path=...` | `SiteAdmin.Themes` | The pages, files and setting that use a file. |
| `GET /check` | `SiteAdmin.Themes` | [Checks](#checking-the-theme) the working copy. Returns the findings, the templates that can be a layout (`layouts`: they write `body`, call `site_head` and have no front matter, whether or not anything uses them yet, so the ones a default layout can be), the files that would change, the default layout of the working copy and of the live release, and the `fingerprint` a publish needs. |
| `POST /publish` | `SiteAdmin.Themes.Publish` | Publishes the working copy, with the check's `fingerprint` and an optional `comment`. |
| `GET /releases` | `SiteAdmin.Themes` | The releases, newest first. |
| `GET /releases/{id}` | `SiteAdmin.Themes` | One release with its file list. |
| `GET /releases/{id}/files/by-path?path=...` | `SiteAdmin.Themes` | One file of a release with its body. |
| `POST /releases/{id}/set-live` | `SiteAdmin.Themes.Publish` | Makes a release live; an older one is the rollback. |
| `POST /releases/{id}/restore` | `SiteAdmin.Themes.Edit` | Copies a release back into the working copy. |
| `GET /preview?path=...&releaseId=...` | `SiteAdmin.Themes` | [Previews](#previewing) the page at the path with the working copy, or with the release when `releaseId` is given. Returns `html`, `path`, `pageName`, `cultureName`, `pageTemplate` (the template rendered, a [version for the page's language](#templates-for-one-language) when it has one) and `layout`. |
| `GET /export?releaseId=...` | `SiteAdmin.Themes` | The working copy, or the release when `releaseId` is given, as a zip; see [Exporting and Importing](#exporting-and-importing). |
| `POST /import` | `SiteAdmin.Themes.Edit` | Brings a zip (`File`, multipart) into the working copy, merged or, with `Replace`, replacing it. |
| `POST /export-links`, `POST /import-links` | `SiteAdmin.Themes`, `SiteAdmin.Themes.Edit` | A one-time link that downloads the export, or that takes a zip as the body of a `PUT` and imports it. |
| `GET /preview-link?path=...&releaseId=...` | `SiteAdmin.Themes` | A link that opens the site at the path, the home page by default, [in a browser](#in-a-browser) with the working copy, or with the release when `releaseId` is given. Returns `url`, `path`, `releaseId` and `expiresAt`. |

A file whose extension no static file may have (`Site:090004`), a save or rename to a path that is a file and a folder at once (`Site:090016`), a rename onto an existing path (`Site:090001`) and a default layout that is not a template path (`Site:090011`), that is not in the working copy (`Site:090012`) or that is not a layout (`Site:090013`) are answered as business errors.

### Exporting and Importing

The whole theme can be taken out as one zip and brought back in: to build its stylesheet where its templates can be read, to bring many files in at once, or to keep a copy elsewhere. In the zip, each file is at its own path, templates without an extension as they are stored, and `.site-theme.json` holds the default layout.

An import goes into the working copy only: nothing goes live until it is checked and published, as any change is. It is one of two kinds:

* **Merge**, the default: the zip's files are added, or replace the file at their path, and the rest of the working copy stays. A zip that holds only `css/site.css` updates the stylesheet.
* **Replace**: the working copy becomes the zip; a file it does not have is deleted.

When `.site-theme.json` names a default layout, it becomes the working copy's. The result lists the files added, modified, deleted and unchanged, and the syntax errors of the templates brought in, which are kept as a save keeps them.

A zip with anything wrong is refused whole, with every problem listed (`Site:090014`): a path outside the theme or not of a theme file's shape, an extension no theme file may have, a file that is not UTF-8 text, a path that would be a file and a folder at once, more than 256 KiB in one file, or more than 1,000 files or 32 MiB in all (`SiteThemePackage.MaxFileCount` and `MaxBytes`). Hidden files, such as `.DS_Store`, and the `__MACOSX/` folder a Mac adds are passed over. The files go at the top of the zip, not inside a folder.

A client that should not carry the bytes itself, such as an AI client over MCP, asks for a **link** instead: good for ten minutes and one use, run as the one who asked for it, like the [upload links of files](../files.md). A download link answers `GET` with the zip; an import link takes the zip as the body of a `PUT`. A link that has expired, has been used, or is not the site's own is refused with `Site:090015`. Behind a gateway, route `api/site-admin/theme/download/*` and `api/site-admin/theme/upload/*` to Site, as `api/site-admin/files/upload/*` is.

### MCP Tools

The MCP server offers the same operations: `site_list_theme_files`, `site_get_theme_file`, `site_save_theme_file`, `site_rename_theme_file`, `site_delete_theme_file`, `site_update_theme`, `site_check_theme`, `site_preview`, `site_get_preview_link`, `site_publish_theme`, `site_list_releases`, `site_set_live_release` and `site_restore_release`, and `site_create_theme_download_url` and `site_create_theme_upload_url` for [exporting and importing](#exporting-and-importing). Files are addressed by path and releases by number. The recommended flow is to save, check and fix every error, preview, and then publish with the check's fingerprint; making an older release live is the rollback. See [Tools](../mcp/tools.md#theme).

## Example

A Scriban equivalent of a minimal Razor `Default` view, which renders a content with its fields, or the list of the page's contents. With `layout = "none"`, or with no default layout, the template is a complete document:

````
<!DOCTYPE html>
<html lang="{{ culture }}">
<head>
    <meta charset="utf-8" />
    <title>{{ head?.meta_title ?? page.display_name }}</title>
    {{ site_head }}
</head>
<body>
<h1>{{ page.display_name }}</h1>
{{ if content }}
    <p>{{ content.publish_time | date.to_string "%Y-%m-%d" }}</p>
    <hr />
    {{ for field in field_list }}
    <section>
        <h2>{{ field.display_name }}</h2>
        {{ render_field field.name }}
    </section>
    {{ end }}
{{ else }}
    {{ for key in (object.keys field_filters) }}<p>{{ key }}: {{ field_filters[key] }}</p>{{ end }}
    {{ list = contents { max: 20 } }}
    <ul>
    {{ for item in list.items }}
        <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
    {{ end }}
    </ul>
    {{ if list.total_count > list.items.size }}<p>{{ list.items.size }} / {{ list.total_count }}</p>{{ end }}
{{ end }}
</body>
</html>
````

The document around the page is the same for every page, so it belongs in a layout, such as the theme's default layout, `layouts/default`:

````
<!DOCTYPE html>
<html lang="{{ culture }}">
<head>
    <meta charset="utf-8" />
    <title>{{ head?.meta_title ?? page.display_name }}</title>
    {{ site_head }}
</head>
<body>
<nav>{{ for lang in languages }}<a href="{{ lang.url }}">{{ lang.culture_name }}</a> {{ end }}</nav>
{{ body }}
</body>
</html>
````

In that layout, the page template writes only what goes inside `<body>`, so the `Default` template shrinks to:

````
<h1>{{ page.display_name }}</h1>
{{ if content }}
    <p>{{ content.publish_time | date.to_string "%Y-%m-%d" }}</p>
    <hr />
    {{ for field in field_list }}
    <section>
        <h2>{{ field.display_name }}</h2>
        {{ render_field field.name }}
    </section>
    {{ end }}
{{ else }}
    {{ for key in (object.keys field_filters) }}<p>{{ key }}: {{ field_filters[key] }}</p>{{ end }}
    {{ list = contents { max: 20 } }}
    <ul>
    {{ for item in list.items }}
        <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
    {{ end }}
    </ul>
    {{ if list.total_count > list.items.size }}<p>{{ list.items.size }} / {{ list.total_count }}</p>{{ end }}
{{ end }}
````

Published with these two files, every page whose `Template` is `Default` renders through Scriban, and every other page keeps its Razor view.

## See Also

* [Razor Templates](razor-templates.md)
* [Multilingual Sites](languages.md)
* [Localizing Template Texts](template-localization.md)
* [SEO](../seo/index.md)
* [MCP Tools](../mcp/tools.md)

}%}
