```json
//[doc-seo]
{
    "Description": "Localize the texts of a Dignite Site's templates, such as option names, labels and archive titles, with per-tenant JSON files next to the templates."
}
```

# Localizing Template Texts

A site's templates show text that is not content: the names of a Select field's options, labels, archive headings. Whoever writes the templates also writes these texts, in JSON files next to the templates, one file per language. Site reads them per tenant, the same way it finds the templates themselves.

## Where the Files Go

Next to the templates, in a `Localization` folder:

| Site | Templates | Texts |
|---|---|---|
| The host (no tenant) | `/Sites/...` | `/Sites/Localization/{culture}.json` |
| A tenant | `/Sites/{tenantName}/...` | `/Sites/{tenantName}/Localization/{culture}.json` |

A tenant reads only its own folder. It never falls back to the host's texts, just as a tenant request never renders the host's `/Sites/` views. A text that a tenant has not written falls back to what the template supplies; see `GetOrDefault` [below](#select-options).

## File Format

The files use ABP's [localization JSON format](https://abp.io/docs/latest/framework/fundamentals/localization). The language is read from `culture`, not from the file name:

````json
{
  "culture": "ja",
  "texts": {
    "Field:blog_category:engineering": "開発",
    "Field:blog_category:news": "ニュース",
    "Archive:Month": "{0:yyyy年M月}の記事"
  }
}
````

Comments and trailing commas are allowed. Name each file `{culture}.json` with exactly one dot (`zh-Hans.json`, not `SiteTemplate.zh-Hans.json`); the next section explains why.

A file that does not parse, or has no `culture`, is skipped and logged as an error. The page then shows the keys, or the template's fallbacks, for those texts; it does not fail.

## Shipping the Files with a Host

The files are read through ABP's [virtual file system](https://abp.io/docs/latest/framework/infrastructure/virtual-file-system). A host that embeds its templates' folder must embed these files too, with an embedded files manifest:

````xml
<PropertyGroup>
  <GenerateEmbeddedFilesManifest>true</GenerateEmbeddedFilesManifest>
</PropertyGroup>
<ItemGroup>
  <EmbeddedResource Include="Sites\**\Localization\*.json" />
  <Content Remove="Sites\**\Localization\*.json" />
</ItemGroup>
````

````csharp
Configure<AbpVirtualFileSystemOptions>(options =>
{
    options.FileSets.AddEmbedded<MyWebModule>();
    if (hostingEnvironment.IsDevelopment())
    {
        options.FileSets.ReplaceEmbeddedByPhysical<MyWebModule>(hostingEnvironment.ContentRootPath);
    }
});
````

Without the manifest, ABP rebuilds an embedded file's path by splitting its resource name on every dot. A file name with a second dot, or a tenant folder name that MSBuild rewrites, such as one containing `-`, would then end up at the wrong path.

## Does a Change Need a Restart?

* **Development**, with physical files such as `ReplaceEmbeddedByPhysical`: no. A saved file is picked up on the next request.
* **Production**, with embedded files: the host has to be rebuilt and redeployed, the same as for the templates themselves, which are compiled.

## Lookup Order

Within the one folder that applies, the tenant's or, when there is no tenant, the host's:

1. The page's language, such as `zh-Hant-TW`.
2. Each parent language in turn: `zh-Hant`, then `zh`.
3. The site's default language, the first entry of the `Site.EnabledLanguages` setting, and its parents.
4. Otherwise the text is not found: `L[key]` shows the key, and `GetOrDefault` shows the template's fallback.

Step 3 applies in every view that MVC renders, Site pages and the host's own pages alike; see [Multilingual Sites](languages.md#template-texts). Outside a view, such as in a background job, it is skipped.

## Using the Texts in a Template

All three localizers work, format arguments included:

````cshtml
@inject IStringLocalizer<SiteTemplateResource> L
@inject IHtmlLocalizer<SiteTemplateResource> H
@inject IViewLocalizer VL   @* inside /Sites/ it reads SiteTemplateResource *@

<p>@L["Archive:Month", Model.PublishedAfter]</p>
````

`IViewLocalizer` reads `SiteTemplateResource` in any view under `/Sites/`, a tenant's included. Every other view keeps ASP.NET Core's usual behavior.

Import `Dignite.Site.Public.Localization` for `SiteTemplateResource`, and `Dignite.Site.Public.Templating` for `GetOrDefault`.

### Select Options

Site does not generate keys: the template chooses them, and maps each option value to its key. The recommended convention is `Field:{fieldName}:{optionValue}`. Use `GetOrDefault`, so that an option the site has not translated still shows the option's own text rather than the key:

````cshtml
@inject SelectFieldOptionsProvider SelectFieldOptions
@inject IStringLocalizer<SiteTemplateResource> L
@{
    var categories = await SelectFieldOptions.GetAsync("blog-post", "blog_category");
}
@foreach (var category in categories)
{
    <a href="/blog/@category.Value">
        @L.GetOrDefault($"Field:blog_category:{category.Value}", category.Text)
    </a>
}
````

On an `IHtmlLocalizer` or `IViewLocalizer`, `GetOrDefault` HTML-encodes the fallback.

### Category and Month Archive Titles

`HeadMetadataBuilder` titles a filtered view `"{page title} - {raw filter values}"`, such as "Blog - engineering". It runs in the API process, where these texts are out of reach, so the template composes a localized title itself:

* `HeadMetadata.BaseMetaTitle` is the title before the filter values were appended.
* The two titles differ only when something was appended. Views that deliberately get no suffix, such as truncated matches or a date filter that cannot filter, keep the plain title.
* `Model.FilterValues` has every captured route value as written, the parts of the publish time included. `Model.FieldFilters` and `Model.PublishedAfter` / `Model.PublishedBefore` are the same values, prepared for querying.

````cshtml
@{
    var head = Model.HeadMetadata;
    Model.FieldFilters.TryGetValue("blog_category", out var category);
    ViewBag.Title = head?.MetaTitle;

    if (head != null && head.MetaTitle != head.BaseMetaTitle)
    {
        if (category != null)
        {
            ViewBag.Title = $"{head.BaseMetaTitle} - {L.GetOrDefault($"Field:blog_category:{category}", category)}";
        }
        // "publishTime:MM" is this route's month placeholder: /blog/2025 is a year, not a month.
        else if (Model.PublishedAfter is { } month && Model.FilterValues.ContainsKey("publishTime:MM"))
        {
            ViewBag.Title = $"{head.BaseMetaTitle} - {L["Archive:Month", month]}";
        }
    }
}
````

Pass the same title to `<site-head-metadata title="@ViewBag.Title" ...>`, so that `og:title` matches `<title>`. A template that sets no title gets the title with the raw values, unchanged.

## See Also

* [Multilingual Sites](languages.md)
