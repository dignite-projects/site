using Asp.Versioning;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dignite.Site.Public.Preview;
using Dignite.Site.Public.Templates;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public.Assets;

/// <summary>
/// Serves the tenant's published asset templates - CSS, JS and other text files saved under <c>assets/</c> - at
/// <c>GET|HEAD /site-assets/{path}</c>: <c>/site-assets/site.css</c> is the published version of
/// <c>assets/site.css</c>, its body as it is. Anonymous, like the pages that link to it; the tenant is the one
/// the request resolves to. Contracts-only, like <see cref="Routing.SiteRenderController"/>: the published bytes
/// come from <see cref="ISiteTemplatePublicAppService"/>.
/// <para>
/// <b>Caching.</b> <c>asset_url</c> writes the id of the published version into the address
/// (<c>?v=</c> and 32 hex digits, <see cref="SiteAssetUrl.VersionQueryKey"/>), so publishing changes every link. A
/// version never changes once saved, so a response whose <c>v</c> is the id of the version it carries is
/// <c>immutable</c> for a year. Any other request - no <c>v</c>, or one that is not the published id - gets
/// <c>no-cache</c>: stored, but revalidated on every use. Pinning those would be wrong: with v3 live, the page
/// links <c>?v=&lt;id3&gt;</c>; after a rollback to v2 it links <c>?v=&lt;id2&gt;</c>, a different address, so
/// nothing stale is ever served under an immutable one. A hand-typed or mismatched <c>v</c> is not a link the
/// site wrote, and gets <c>no-cache</c> and the live bytes. The id and not the version number, because numbers
/// restart at 1 when a template is deleted and created again under the same name: <c>?v=1</c> could then name
/// other bytes than the ones a browser or CDN kept for a year, while an id is never reused. The <c>ETag</c> is the
/// same id, so a revalidating cache gets a 304 while the bytes are unchanged.
/// </para>
/// <para>
/// <c>X-Content-Type-Options: nosniff</c> keeps a browser to the declared type, which comes from the extension
/// (<see cref="GetContentType"/>), so a text file can never be run as a script under another type.
/// </para>
/// <para>
/// A literal first segment outranks the catch-alls of the same order - <see cref="Seo.FeedController"/>'s - and
/// this route keeps the default <c>Order = 0</c>, so <see cref="Routing.SiteRenderController"/>'s
/// <c>Order = 1</c> catch-all never sees <c>/site-assets/</c>: an unknown asset is a 404 here, not a page lookup.
/// </para>
/// <para>
/// <b>Draft mode.</b> A visitor whose preview cookie names an asset (<see cref="SiteTemplatePreviewState"/>) gets that
/// version's body at its address instead - <c>no-store</c> and <c>noindex</c>, so no cache ever holds it and nobody
/// else is ever served it.
/// </para>
/// </summary>
[ControllerName(ControllerName)]
public class SiteAssetController : AbpController
{
    public const string ControllerName = "SiteAsset";

    /// <summary>A year, the longest a cache is asked to keep anything; the address changes before it matters.</summary>
    public const int ImmutableMaxAgeSeconds = 365 * 24 * 60 * 60;

    protected ISiteTemplatePublicAppService TemplateAppService { get; }
    protected SiteTemplatePreviewState PreviewState { get; }

    public SiteAssetController(ISiteTemplatePublicAppService templateAppService, SiteTemplatePreviewState previewState)
    {
        TemplateAppService = templateAppService;
        PreviewState = previewState;
    }

    [AcceptVerbs("GET", "HEAD", Route = "/" + SiteAssetUrl.RoutePrefix + "/{**path}")]
    public virtual async Task<IActionResult> GetAsync(string? path)
    {
        if (string.IsNullOrEmpty(path) || !IsValidPath(path))
        {
            return NotFound();
        }

        var name = SiteTemplateNames.GetAssetName(path);

        var previewVersion = await FindPreviewVersionAsync(name);
        if (previewVersion != null)
        {
            return Serve(previewVersion, path, preview: true);
        }

        var version = await TemplateAppService.FindPublishedAsync(name);
        if (version == null)
        {
            return NotFound();
        }

        return Serve(version, path, preview: false);
    }

    /// <summary>
    /// The version of <paramref name="name"/> the visitor's draft mode puts in place of the published one: only for
    /// a valid preview cookie whose ticket names this very asset (<see cref="SiteTemplatePreviewState"/>), null for
    /// everyone else - who keep getting the published bytes, whatever <c>v</c> they ask for.
    /// </summary>
    protected virtual async Task<SiteTemplateVersionDto?> FindPreviewVersionAsync(string name)
    {
        var ticket = PreviewState.Ticket;
        if (ticket == null
            || ticket.Kind != SiteTemplateKind.Asset
            || !string.Equals(ticket.TemplateName, name, StringComparison.Ordinal))
        {
            return null;
        }

        return await PreviewState.GetOverrideVersionAsync(HttpContext.RequestAborted);
    }

    /// <summary>
    /// <paramref name="version"/>'s body, typed by <paramref name="path"/>'s extension. A previewed version is never
    /// stored by any cache and never indexed (<see cref="SiteTemplatePreviewHttp.ApplyHeaders"/>); a published one is
    /// cached as the class remarks describe.
    /// </summary>
    protected virtual IActionResult Serve(SiteTemplateVersionDto version, string path, bool preview)
    {
        var entityTag = new EntityTagHeaderValue("\"" + version.Id.ToString("N") + "\"");

        if (preview)
        {
            Response.Headers.ETag = entityTag.ToString();
            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            SiteTemplatePreviewHttp.ApplyHeaders(Response);
        }
        else
        {
            SetCacheHeaders(entityTag, IsVersioned(version.Id));
        }

        if (IsNotModified(entityTag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        // No BOM: the charset is declared, and a BOM before a stylesheet's @charset invalidates it.
        var body = new UTF8Encoding(false).GetBytes(version.Body ?? string.Empty);
        return File(body, GetContentType(path));
    }

    /// <summary>
    /// The <c>Content-Type</c> for an asset, by its extension; text that is none of these is served as plain
    /// text. A host serving another kind of text file overrides this.
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
    /// Whether <paramref name="path"/> - the address after <c>/site-assets/</c> - could name an asset. The rule
    /// that saves a template (<see cref="SiteTemplateConsts.NamePattern"/>) applied to the full name, so what can
    /// be saved is what can be served and nothing else - no <c>..</c>, no empty segment, no hidden-file name. Only a
    /// filter that spares the query for nonsense; whether an asset of that name exists is the application
    /// service's answer.
    /// </summary>
    protected virtual bool IsValidPath(string path)
    {
        var name = SiteTemplateNames.GetAssetName(path);

        // The name pattern lets an empty segment through ("a//b"), and the name is normalized (trimmed, a leading
        // slash dropped) on the way in; an address is exact, so neither is a path that names an asset.
        return name.Length <= SiteTemplateConsts.MaxNameLength
               && name == SiteTemplateConsts.AssetsPrefix + path
               && !path.Contains("//", StringComparison.Ordinal)
               && !path.EndsWith('/')
               && Regex.IsMatch(name, SiteTemplateConsts.NamePattern, RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Whether the request's <c>v</c> is <paramref name="publishedVersionId"/> as 32 hex digits, ignoring case -
    /// see the class remarks.
    /// </summary>
    protected virtual bool IsVersioned(Guid publishedVersionId)
    {
        // Parsed, not compared as text: asset_url writes the id without dashes, but the id is the same id in any
        // of Guid's spellings, and a hand-written dashed one deserves the same answer.
        return Guid.TryParse(Request.Query[SiteAssetUrl.VersionQueryKey].ToString(), out var requested)
               && requested == publishedVersionId;
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
