using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Site.Public.Files;

/// <summary>
/// Serves the site's files to anyone - the address a content field stores
/// (<c>/api/site-public/files/{containerName}/{blobName}?__tenant=</c>). Only Site's own containers
/// (<c>SiteFileContainerNames</c>) are served; any other container, a file that does not exist and a deleted
/// one are all simply not found.
/// </summary>
public interface IFilePublicAppService : IApplicationService
{
    Task<IRemoteStreamContent> GetAsync(string containerName, string blobName, GetFileInput input);
}
