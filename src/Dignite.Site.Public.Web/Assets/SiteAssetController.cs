using Asp.Versioning;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dignite.Site.Public.Preview;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public.Assets;

/// <summary>
/// Serves the static files of the tenant's live release - CSS, JS and other text files, any file of the theme whose
/// name has an extension - at <c>GET|HEAD /site-assets/{path}</c>: <c>/site-assets/css/site.css</c> is the live
/// release's <c>css/site.css</c>, its body as it is. Anonymous, like the pages that link to it; the tenant is the one
/// the request resolves to. Contracts-only, like <see cref="Routing.SiteRenderController"/>: the one dependency is
/// <see cref="ISiteThemePublicAppService"/>, which only ever reads published content - a file of the working copy is
/// never served here.
/// <para>
/// <b>Caching.</b> <c>asset_url</c> writes the content's SHA-256 into the address (<c>?v=</c>,
/// <see cref="SiteAssetUrl.VersionQueryKey"/>), so a change of content changes every link to it. A response whose
/// <c>v</c> is the hash of the content it carries is <c>immutable</c> for a year: those bytes can never be served under
/// that address differently. Any other request - no <c>v</c>, or one that is not the live content's - gets
/// <c>no-cache</c>: stored, but revalidated on every use, since the live content of a path changes with every
/// release. The <c>ETag</c> is the same hash, so a revalidating cache gets a 304 while the bytes are unchanged.
/// </para>
/// <para>
/// <b>Preview.</b> A visitor holding a valid preview cookie (<see cref="SitePreviewState"/>) is served the static files of
/// the theme it names - the working copy or a release - instead, with <c>no-store</c> and <c>noindex</c>: those bytes
/// are for the author only, and no cache may keep them.
/// </para>
/// <para>
/// <c>X-Content-Type-Options: nosniff</c> keeps a browser to the declared type, which comes from the extension
/// (<see cref="GetContentType"/>), so a text file can never be run as a script under another type.
/// </para>
/// <para>
/// A literal first segment outranks the catch-alls of the same order - <see cref="Seo.FeedController"/>'s - and
/// this route keeps the default <c>Order = 0</c>, so <see cref="Routing.SiteRenderController"/>'s
/// <c>Order = 1</c> catch-all never sees <c>/site-assets/</c>: an unknown file is a 404 here, not a page lookup.
/// </para>
/// </summary>
[ControllerName(ControllerName)]
public class SiteAssetController : AbpController
{
    public const string ControllerName = "SiteAsset";

    /// <summary>A year, the longest a cache is asked to keep anything; the address changes before it matters.</summary>
    public const int ImmutableMaxAgeSeconds = 365 * 24 * 60 * 60;

    protected ISiteThemePublicAppService ThemeAppService { get; }

    public SiteAssetController(ISiteThemePublicAppService themeAppService)
    {
        ThemeAppService = themeAppService;
    }

    [AcceptVerbs("GET", "HEAD", Route = "/" + SiteAssetUrl.RoutePrefix + "/{**path}")]
    public virtual async Task<IActionResult> GetAsync(string? path)
    {
        if (string.IsNullOrEmpty(path) || !IsValidPath(path))
        {
            return NotFound();
        }

        var preview = LazyServiceProvider.LazyGetRequiredService<SitePreviewState>();
        if (preview.IsAvailable)
        {
            return await GetPreviewAssetAsync(preview, path);
        }

        var asset = await ThemeAppService.FindLiveAssetAsync(path);
        if (asset == null)
        {
            return NotFound();
        }

        var entityTag = new EntityTagHeaderValue("\"" + asset.ContentHash + "\"");
        SetCacheHeaders(entityTag, IsVersioned(asset.ContentHash));

        if (IsNotModified(entityTag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        // No BOM: the charset is declared, and a BOM before a stylesheet's @charset invalidates it.
        var body = new UTF8Encoding(false).GetBytes(asset.Body ?? string.Empty);
        return File(body, GetContentType(path));
    }

    /// <summary>A static file of the previewed theme - see the class remarks. No validators: nothing of it is stored.</summary>
    protected virtual async Task<IActionResult> GetPreviewAssetAsync(SitePreviewState preview, string path)
    {
        preview.ApplyResponseHeaders(Response);
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

        var asset = await preview.FindAssetAsync(path);
        if (asset == null)
        {
            return NotFound();
        }

        return File(new UTF8Encoding(false).GetBytes(asset.Body ?? string.Empty), GetContentType(path));
    }

    /// <summary>
    /// The <c>Content-Type</c> for a static file, by its extension; text that is none of these is served as plain
    /// text. A host serving another kind of text file adds its extension to <see cref="SiteThemeConsts.AssetExtensions"/>
    /// and overrides this.
    /// </summary>
    protected virtual string GetContentType(string path)
    {
        var mediaType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".css" => "text/css",
            ".js" or ".mjs" => "text/javascript",
            ".json" or ".map" => "application/json",
            ".txt" => "text/plain",
            ".svg" => "image/svg+xml",
            ".xml" => "application/xml",
            ".webmanifest" => "application/manifest+json",
            ".ics" => "text/calendar",
            ".csv" => "text/csv",
            _ => "text/plain"
        };

        return mediaType + "; charset=utf-8";
    }

    /// <summary>
    /// Whether <paramref name="path"/> - the address after <c>/site-assets/</c> - could name a static file: the rule a
    /// theme file's path is saved under (<see cref="SiteThemePaths.IsValid"/>), with an extension, and exactly as stored
    /// (no leading slash, no surrounding space). Only a filter that spares the query for nonsense; whether the live
    /// release has such a file is the application service's answer.
    /// </summary>
    protected virtual bool IsValidPath(string path)
    {
        return SiteThemePaths.Normalize(path) == path
               && SiteThemePaths.IsValid(path)
               && SiteThemePaths.IsAsset(path);
    }

    /// <summary>Whether the request's <c>v</c> is <paramref name="contentHash"/>, ignoring case - see the class remarks.</summary>
    protected virtual bool IsVersioned(string contentHash)
    {
        return string.Equals(Request.Query[SiteAssetUrl.VersionQueryKey].ToString(), contentHash, StringComparison.OrdinalIgnoreCase);
    }

    protected virtual bool IsNotModified(EntityTagHeaderValue entityTag)
    {
        var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch == null || ifNoneMatch.Count == 0)
        {
            return false;
        }

        foreach (var candidate in ifNoneMatch)
        {
            // If-None-Match compares weakly (RFC 9110, 13.1.2).
            if (candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(entityTag, useStrongComparison: false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Set on a 200 and on a 304 alike: a 304 refreshes what the cache stored.</summary>
    protected virtual void SetCacheHeaders(EntityTagHeaderValue entityTag, bool versioned)
    {
        Response.Headers.ETag = entityTag.ToString();
        Response.Headers.CacheControl = versioned
            ? $"public, max-age={ImmutableMaxAgeSeconds.ToString(CultureInfo.InvariantCulture)}, immutable"
            : "public, no-cache";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
    }
}
