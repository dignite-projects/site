using System;
using System.Globalization;
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
/// save it under its name. Anonymous; the tenant comes from the address's <c>__tenant</c>.
/// <para>
/// <b>Caching.</b> A blob name is generated, never reused and never overwritten, so the bytes behind an
/// address never change: the address plus the requested size is a strong validator. The response carries it
/// as the <c>ETag</c>, and a matching <c>If-None-Match</c> is answered <c>304</c> without touching the
/// database or the blob. <c>Cache-Control</c> is public for a day rather than <c>immutable</c>, because a file
/// can be deleted, and a day bounds how long a shared cache keeps serving one that was.
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
        var input = new GetFileInput();
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

        // Quoted-string content may not contain '"' or '\'; a blob name is generated, but be strict anyway.
        var opaque = (blobName + size).Replace("\"", string.Empty).Replace("\\", string.Empty);
        return new EntityTagHeaderValue($"\"{opaque}\"");
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
