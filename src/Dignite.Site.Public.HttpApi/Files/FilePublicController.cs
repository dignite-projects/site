using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Site.Public.Files;

/// <summary>
/// The address every Site file is served at: <c>GET|HEAD /api/site-public/files/{containerName}/{blobName}</c>
/// (optionally <c>?Width=&amp;Height=</c> for an image), and <c>.../download/{containerName}/{blobName}</c> to
/// save it under its name. Anonymous; the tenant is the request's, resolved like any other (the host name, a header, ...).
/// <para>
/// <b>Caching.</b> A blob name is generated, never reused and never overwritten, so the bytes behind an
/// address never change: the address plus the requested size is a strong validator. The response carries it
/// as the <c>ETag</c>, and a matching <c>If-None-Match</c> is answered <c>304</c> without touching the
/// database or the blob. <c>Cache-Control</c> is public for a day rather than <c>immutable</c>, because a file
/// can be deleted, and a day bounds how long a shared cache keeps serving one that was.
/// </para>
/// <para>
/// <b>WebP.</b> Unless <c>?Format=</c> says otherwise, a JPEG or PNG is served as WebP to a request whose
/// <c>Accept</c> lists <c>image/webp</c>, and a stored WebP as JPEG to one that does not; so the address of a JPEG,
/// PNG or WebP answers with <c>Vary: Accept</c> and the ETag says which it is, and any other file's address does
/// neither. The download address serves the stored file, whatever the request takes.
/// </para>
/// </summary>
[RemoteService(Name = SitePublicRemoteServiceConsts.RemoteServiceName)]
[Area(SitePublicRemoteServiceConsts.ModuleName)]
[Route(SiteFileUrl.RoutePrefix)]
public class FilePublicController : SitePublicController, IFilePublicAppService
{
    public const int MaxAgeSeconds = 24 * 60 * 60;

    protected IFilePublicAppService FilePublicAppService { get; }

    public FilePublicController(IFilePublicAppService filePublicAppService)
    {
        FilePublicAppService = filePublicAppService;
    }

    [HttpGet]
    [HttpHead]
    [Route("{containerName}/{*blobName}")]
    public virtual async Task<IRemoteStreamContent> GetAsync(string containerName, string blobName, GetFileInput input)
    {
        // Auto is decided here, the one place that sees the request; the application service gets a format. A public
        // web application that calls Site through the client proxy decides it from its own visitor's Accept, and
        // the proxy passes the decided format on.
        var format = SiteFileFormats.Normalize(input.Format);
        if (format == SiteFileFormats.Auto)
        {
            input.Format = AcceptsWebp() ? SiteFileFormats.Webp : SiteFileFormats.Jpeg;
            if (MayBeConverted(blobName))
            {
                Response.Headers.Append(HeaderNames.Vary, HeaderNames.Accept);
            }
        }
        else
        {
            input.Format = format;
        }

        var entityTag = CreateEntityTag(blobName, input);
        if (IsNotModified(entityTag))
        {
            return NotModified(entityTag)!;
        }

        var content = await FilePublicAppService.GetAsync(containerName, blobName, input);
        SetCacheHeaders(entityTag);
        SetContentDisposition("inline", content.FileName);
        return content;
    }

    /// <summary>
    /// The file as an attachment, named <paramref name="fileName"/> when given, else its own name.
    /// </summary>
    [HttpGet]
    [HttpHead]
    [Route("download/{containerName}/{*blobName}")]
    public virtual async Task<IRemoteStreamContent> DownloadAsync(string containerName, string blobName, string? fileName = null)
    {
        var input = new GetFileInput { Format = SiteFileFormats.Original };
        var entityTag = CreateEntityTag(blobName, input);
        if (IsNotModified(entityTag))
        {
            return NotModified(entityTag)!;
        }

        var content = await FilePublicAppService.GetAsync(containerName, blobName, input);
        SetCacheHeaders(entityTag);
        SetContentDisposition("attachment", fileName.IsNullOrWhiteSpace() ? content.FileName : fileName);
        return content;
    }

    protected virtual EntityTagHeaderValue CreateEntityTag(string blobName, GetFileInput input)
    {
        var size = (input.Width ?? 0) > 0 || (input.Height ?? 0) > 0
            ? $"-{(input.Width ?? 0).ToString(CultureInfo.InvariantCulture)}x{(input.Height ?? 0).ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        var format = input.Format is SiteFileFormats.Webp or SiteFileFormats.Jpeg && MayBeConverted(blobName)
            ? "-" + input.Format
            : string.Empty;

        // Quoted-string content may not contain '"' or '\'; a blob name is generated, but be strict anyway.
        var opaque = (blobName + size + format).Replace("\"", string.Empty).Replace("\\", string.Empty);
        return new EntityTagHeaderValue($"\"{opaque}\"");
    }

    /// <summary>
    /// Whether the file at <paramref name="blobName"/> may be served in another format than it is stored in, told from
    /// the name alone so a <c>304</c> needs no lookup: a blob name ends in the extension of its stored type; a JPEG or
    /// PNG is converted to WebP, a WebP to JPEG. A name without an extension, as an early version gave, may be any.
    /// </summary>
    protected virtual bool MayBeConverted(string blobName)
    {
        var extension = Path.GetExtension(blobName);
        return extension.IsNullOrEmpty() ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether the request's <c>Accept</c> header lists <c>image/webp</c> (and does not refuse it with
    /// <c>q=0</c>). A wildcard does not count: <c>*/*</c> is what a crawler that reads only JPEG and PNG sends too.
    /// </summary>
    protected virtual bool AcceptsWebp()
    {
        IList<MediaTypeHeaderValue> accept;
        try
        {
            accept = Request.GetTypedHeaders().Accept;
        }
        catch (FormatException)
        {
            return false;
        }

        foreach (var mediaType in accept)
        {
            if (mediaType.MediaType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            {
                return mediaType.Quality is not 0d;
            }
        }

        return false;
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
            if (candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(entityTag, useStrongComparison: false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A <c>304</c>: the status and validators are set here, and the <c>null</c> result is written by MVC's
    /// no-content formatter, which leaves a non-200 status as it is.
    /// </summary>
    protected virtual IRemoteStreamContent? NotModified(EntityTagHeaderValue entityTag)
    {
        SetCacheHeaders(entityTag);
        Response.StatusCode = StatusCodes.Status304NotModified;
        return null;
    }

    protected virtual void SetCacheHeaders(EntityTagHeaderValue entityTag)
    {
        var headers = Response.GetTypedHeaders();
        headers.ETag = entityTag;
        headers.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(MaxAgeSeconds)
        };

        // The declared type is the detected one; never let a browser second-guess it into something
        // executable on the site's own origin.
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

        // A response meant for a shared cache sets no cookie: a cache may refuse to store a response with Set-Cookie, or
        // store it and hand the cookie to every visitor after - one visitor's culture and tenant for everyone.
        // The host's tenant and localization middleware append theirs before this action runs.
        Response.Headers.Remove(HeaderNames.SetCookie);
    }

    protected virtual void SetContentDisposition(string type, string? fileName)
    {
        var disposition = new ContentDispositionHeaderValue(type);
        if (!fileName.IsNullOrWhiteSpace())
        {
            disposition.SetHttpFileName(fileName);
        }

        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
    }
}
