using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.BlobStoring;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Sets the upload request's body limit to the target container's size limit (plus room for the multipart
/// envelope) before the body is read, so an oversized upload is refused at the HTTP layer - by the server,
/// before it is buffered - rather than after. The limit is the container's <c>MaxSize</c> contributor, or
/// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/> without one - the same bound
/// <c>FileDescriptorManager</c> buffers the upload to and the pipeline enforces while saving. This is the
/// request-body limit at the HTTP layer that <c>Dignite.Abp.BlobStoring.Pipeline</c> still expects a host to keep.
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
            var maxSizeInBytes = BlobStreamBuffering.GetMaxBufferedBytes(_configurationProvider.Get(containerName));

            var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = checked(maxSizeInBytes + MultipartOverheadBytes);
            }
        }

        await next();
    }
}
