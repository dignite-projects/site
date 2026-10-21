```json
//[doc-seo]
{
    "Description": "Pages are the route table of a Dignite Site: route templates with slug, field and date placeholders, optional segments, the home page, and how a request path is resolved."
}
```

# Pages and Routing

A **page** is a node of the site's route table. It owns a route, the [content types](content-types-and-fields.md) defined beneath it, and the template that renders it. The pages of a site are its authoritative route table: the sitemap, the canonical URLs, the hreflang alternates and the feeds are all derived from them, whatever front end renders the pages.

A page has no "kind". It is not declared as a single page, a list or a detail page: its route says whether it has contents beneath it, and the template decides what to render for each request.

## Page Properties

| Property | Description |
|---|---|
| `Name` | A machine name, unique in the site, such as `blog`. The MCP tools and the templates address a page by it. Lowercase letters and digits, with `-` or `_` as separators. |
| `DisplayName` | The human-readable name, such as "Blog". |
| `Route` | The route template, starting with `/`. See [Route Templates](#route-templates). Unique in the site. |
| `Template` | The MVC view that renders the page, such as `Default`. See [Templates](rendering/templates.md). |
| `ParentId` | The page this one is organized under in the admin UI's tree. Purely organizational: it has no effect on the route, and a child page can have a route unrelated to its parent's. |
| `IsActive` | An inactive page is not routed and is left out of the sitemap, but keeps its contents. |

A page with child pages cannot be deleted; move or delete the children first. Deleting a page deletes its content types and all of their contents, in every language.

A new page comes with one content type, named after the page and carrying the [SEO field](field-types.md#seo). Arrange its fields, or add more content types, from there.

## Route Templates

A route is a **template**, not only a base path. The slug placeholder decides whether a page has contents beneath it:

| Route | Contents beneath it |
|---|---|
| `/about` | No slug: the page holds one content per language, with an empty slug, served at `/about`. |
| `/blog/{slug}` | Every content has a slug: `/blog/my-trip`. `/blog` is the page itself, typically rendered as a list. |
| `/about/{slug?}` | Contents with a slug, `/about/history`, plus one content with an empty slug, served at `/about`. |
| `/{slug:^[a-z-]+$}` | Every content has a slug, and the slug must also match the regular expression. |

A slug with a regular expression gives a few contents root-level addresses without the page answering every other root-level path, as `/{slug}` would:

````text
/{slug:^(privacy-policy|terms-of-service)$}
````

`{slug}`, `{slug?}` or `{slug:REGEX}` must be the last placeholder of the route. A content whose slug breaks the route's rule is refused when it is saved:

| Error | When |
|---|---|
| `Site:040004` | The route has no slug placeholder, and a slug was given. |
| `Site:040005` | The route requires a slug (`{slug}`), and the slug is empty. |
| `Site:040006` | The slug does not match the route's regular expression. |

### Field Placeholders

A route can also contain placeholders named after any field of the content: the system field `publishTime`, or a field of the content type. Each one has four forms:

| Form | Example | Meaning |
|---|---|---|
| `{name}` | `{category}` | The value as it is. |
| `{name:FORMAT}` | `{publishTime:yyyy-MM}` | The value formatted with a .NET format string when a URL is built. |
| `{name:REGEX}` | `{category:^[a-z-]+$}` | The value a request names must match the regular expression. |
| `{name:FORMAT:REGEX}` | `{publishTime:yyyy-MM:^\d{4}-\d{2}$}` | Both. |

With a single `:`, the text is a FORMAT if it contains only letters, digits, `.`, `_` and `-`, and a REGEX otherwise; anchor a regular expression with `^...$` so it is never read as a format. Neither may contain `/`, which separates the segments of a path.

`/news/{publishTime:yyyy-MM}/{slug}` puts a content published in July 2026 at `/news/2026-07/my-post`.

### Optional Placeholders

Put `?` right after a placeholder's name to make it optional: `{name?}`, `{name?:FORMAT}`, `{name?:REGEX}` or `{name?:FORMAT:REGEX}`. A request may then leave out the whole segment:

````text
/news/{category?:^(news|tutorials)$}/{publishTime?:yyyy:^\d{4}$}/{publishTime?:MM:^(0[1-9]|1[0-2])$}
````

This route answers `/news/tutorials`, `/news/2026` and `/news/2026/08`. An optional placeholder must fill a whole segment on its own, every optional placeholder but the last needs a REGEX (that is what decides which segment belongs to which placeholder), and a route can have at most four. `{slug?}` keeps its own meaning, described above.

### The Page's Own Address

A page's own address is the part of its route before the first placeholder, up to the last complete segment: `/blog/{slug}` is at `/blog`, `/news/{publishTime:yyyy-MM}/{slug}` is at `/news`, and a route without placeholders is at its own path. The sitemap, the feeds and the canonical URL of the page are built from this address.

### The Home Page

A page whose own address is `/` and whose route does not require a slug is the home page: `/` and `/{slug?}` are home routes, while `/{slug}` and `/{slug:REGEX}` are not, since every content beneath them has its own root-level address. The home page is always at the root of the admin UI's page tree.

## Resolving a Request

A request path is resolved in these steps:

1. A language prefix of one of the site's non-default languages is stripped, and sets the language of the request; see [Multilingual Sites](rendering/languages.md).
2. The path is cut back one segment at a time, from the whole path down to `/`. At each length, the pages whose own address equals that prefix are the candidates.
3. A candidate whose placeholders can read the **whole** path wins first. With a slug, that is a content beneath the page.
4. When a single candidate is at that length, it can also read the path with its last placeholders left out: `/blog/2026-08` against `/blog/{publishTime:yyyy-MM}/{slug}` is the page itself, filtered by those values. With two or more candidates, this step is skipped, so a page made specifically for an address always wins over a deeper route that only guesses at it.
5. Otherwise a page whose route has no placeholders is matched at its own address, which resolves to its empty-slug content, if it has one, or to the page itself.
6. The first length at which a page is found decides the request; shorter prefixes are not tried. A page at `/` takes part in step 3 only, so an unknown path never lands on the home page.

A request resolves to one of three results:

| Result | Example | Renders |
|---|---|---|
| The page | `/blog`, or `/blog/2026-08` with filter values | Typically a list of the page's contents, filtered by the values the path named. |
| The page's own content | `/about` | The content with an empty slug. |
| A content | `/blog/my-trip` | The content with that slug, in the request's language. |

A content is resolved only when it is published and its publish time has passed; a draft, a scheduled or an archived content is never served at its address. The `site_resolve_path` [MCP tool](mcp/tools.md) and the `/api/site-public/resolve-path` endpoint return the same result.

## Changing a Route

Changing a page's route changes the URL of every content beneath it, and old links stop working. Contents that no longer fit the new route, such as a content with a slug on a route that now has none, are not re-validated: the next save of each one applies the new rules.

## See Also

* [Content Types and Fields](content-types-and-fields.md)
* [Contents](contents.md)
* [Templates](rendering/templates.md)
