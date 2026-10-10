using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.BlobStoring;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Sets the upload request's body limit to the target container's file size limit (plus room for the
/// multipart envelope) before the body is read, so an oversized upload is refused at the HTTP layer - by the
/// server, before it is buffered - rather than after. <c>IFileStorer</c> still enforces the limit itself
/// while copying; this is the outer bound file-storing's invariant §1 expects a host to keep.
/// </summary>
public class FileUploadSizeLimitFilter : IAsyncResourceFilter
{
    private const long MultipartOverheadBytes = 64 * 1024;

    private readonly IBlobContainerConfigurationProvider _configurationProvider;

    public FileUploadSizeLimitFilter(IBlobContainerConfigurationProvider configurationProvider)
    {
        _configurationProvider = configurationProvider;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var containerName = context.HttpContext.Request.Query["containerName"].ToString();

        // Only Site's containers: anything else is refused by the application service anyway, and must not
        // get a limit raised on its behalf.
        if (SiteFileContainerNames.Contains(containerName))
        {
            var maxFileSizeInBytes = _configurationProvider
                .Get(containerName)
                .GetFileSizeLimitConfiguration()
                .MaxFileSizeInBytes;
            if (maxFileSizeInBytes <= 0)
            {
                maxFileSizeInBytes = FileConsts.DefaultMaxFileSizeInBytes;
            }

            var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = checked(maxFileSizeInBytes + MultipartOverheadBytes);
            }
        }

        await next();
    }
}
