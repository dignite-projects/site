```json
//[doc-seo]
{
    "Description": "Render a multilingual Dignite Site: language URL prefixes, in-site links in the page's language, a language switcher and an optional redirect of / to the visitor's language."
}
```

# Multilingual Sites

A site's languages are the `Site.EnabledLanguages` setting, in order. The first one is the **default language**, and its URLs have no prefix; the others live under `/{culture}/`, as in `/ja/about`. Site applies these rules to every URL it emits (the sitemap, feeds, canonical URLs and hreflang alternates) and to every request it routes.

`Dignite.Site.Public.Web` gives templates and the host's layout the same rules, so a host that renders more than one language does not have to restate them:

* [In-site links](#in-site-links) get the language prefix of the page they are on.
* A [language switcher](#language-switcher) links to the page's translations.
* The home page can [redirect to the visitor's language](#redirecting-the-home-page).
* [Template texts](template-localization.md) fall back to the site's default language.

## Setup

Nothing goes into the host's middleware pipeline. `SitePublicWebModule` registers two global MVC filters, and every helper on this page runs inside MVC, after `UseMultiTenancy()` has resolved the tenant whose setting applies. Templates only need the namespace and the tag helpers:

````cshtml
@using Dignite.Site.Public.Localization
@addTagHelper *, Dignite.Site.Public.Web
````

The options are all optional:

````csharp
Configure<SiteLanguageOptions>(options =>
{
    // Paths that are not Site pages: in-site links to them never get a language prefix.
    // Default: /Account, /Abp, /api, /connect, /swagger, /Error, /.well-known, /site-assets. Matched per whole
    // segment, ignoring case and slashes. Anything whose last segment has a dot is left alone as well.
    options.ExcludedPathPrefixes.Add("/Contact");

    // Send "/" to the visitor's language. Off by default; see below.
    options.RedirectHomeToVisitorLanguage = true;
    options.CookieName = "site-lang"; // the default
});
````

## The Language of a Page

The request path decides. `/ja/about` is in Japanese if the site serves Japanese as a non-default language; anything else, including `/de/about` on a site without German, is in the default language. This is the rule routing uses.

`CultureInfo.CurrentUICulture` is not used: outside a Site page it reflects the culture cookie of the administration side, not the language the visitor reads the site in.

An error page rendered by re-executing the request (`UseStatusCodePagesWithReExecute`, `UseExceptionHandler`) takes the language of the page that failed. A redirect to an error page, as ABP's `UseErrorPage()` does, loses it; that is the host's flow to change.

## In-Site Links

Write app-relative links as usual. On a page in a non-default language, they get that language's prefix:

````cshtml
@* on /ja/about: href="/ja/services" *@
<a href="~/services">Services</a>
<a href="~/blog/@post.Slug?tab=2">...</a>
<form action="~/search" method="get">...</form>
````

This covers every attribute that navigates: `<a href>`, `<area href>`, `<form action>`, and `formaction` on `<button>` and `<input>`.

These links are left unchanged:

* Every link on a page in the default language, so a single-language site never has a link rewritten.
* Paths under `ExcludedPathPrefixes`, and file-like paths such as `~/favicon.ico`.
* Links that already carry a language prefix, such as `~/zh-Hans/about`.
* Links that are not app-relative. `href="@Url.Content("~/about")"` is already `/about` when the tag helper sees it, which is how to link to the default language on purpose.

For a path built in code, such as the URL of a menu item, use `Url.SiteContent`:

````cshtml
<a href="@Url.SiteContent("~/blog/" + category)">...</a>
<a href="@Url.SiteContent("~/about", "en")">English</a>   @* one language in particular *@
````

`Url.SiteContent` is synchronous. It works in any view, because the languages are loaded before a view renders; on a Site page they come with the route match, without reading the setting again. In a controller action there is no view yet, so await the service instead:

````csharp
var cultureName = await siteLanguageContext.GetCurrentCultureNameAsync();
````

## Language Switcher

`SiteLanguageSwitcher` returns one item per enabled language, in the configured order. On a Site page, an item links to the page's own translation when it has one, and to that language's home page when it does not. Translations come from the page's hreflang alternates, so a translation that is `noindex` or unpublished counts as missing. On any other page, every item links to a home page. The labels and the markup are the host's:

````cshtml
@inject SiteLanguageSwitcher Switcher
@using System.Globalization

<nav aria-label="Language">
  @foreach (var item in await Switcher.GetItemsAsync())
  {
      var culture = CultureInfo.GetCultureInfo(item.CultureName);
      <a href="@item.Url" hreflang="@item.CultureName" lang="@item.CultureName"
         aria-current="@(item.IsCurrent ? "true" : null)" data-site-lang="@item.CultureName">
        @culture.TextInfo.ToTitleCase(culture.NativeName)
      </a>
  }
</nav>
````

`item.Url` is host-relative, such as `/ja/blog/my-trip`: the request's `PathBase` plus the page's path in the site, which the server sends with each hreflang alternate (`HreflangAlternateDto.Path`). Links stay on the host the visitor is on, whatever base path the primary domain is configured with.

## Redirecting the Home Page

With `RedirectHomeToVisitorLanguage` on, a `GET` or `HEAD` request for `/` is redirected (`302`) to the home page of a language. The rules are checked in this order:

1. A navigation from within the site (`Sec-Fetch-Site: same-origin`) is never redirected: the visitor followed a link to `/` and gets `/`.
2. The `CookieName` cookie, if it names a language the site serves. If it names the default language, the visitor stays on `/`.
3. Otherwise `Accept-Language`, by quality. A tag matches through its parent cultures (`zh-TW` reaches `zh-Hant`), then by a shared root language (`en-US` reaches `en-GB`). The root keeps the script, so the fallback never crosses scripts: `zh-TW` does not reach `zh-Hans`.

If the result is the default language, or nothing matches, `/` is served as it is. A crawler sends no `Accept-Language`, so it stays on `/`. Only `/` is redirected, and the response carries `Vary: Accept-Language, Cookie, Sec-Fetch-Site`.

Site reads the cookie but never writes it. To make a visitor's choice stick across visits, set it when they pick a language in the switcher, for example:

````js
document.querySelectorAll('[data-site-lang]').forEach(function (a) {
  a.addEventListener('click', function () {
    document.cookie = 'site-lang=' + encodeURIComponent(a.dataset.siteLang) +
      '; path=/; max-age=31536000; samesite=lax';
  });
});
````

## Template Texts

Every view that MVC renders, not only Site pages, falls back to the site's default language for [template texts](template-localization.md). A host's menu on an error page requested in a language the site does not serve shows the texts of the default language, not their keys.

## See Also

* [Localizing Template Texts](template-localization.md)
