using Volo.Abp;

namespace Dignite.Site.Files;

/// <summary>
/// The file library works only in <see cref="SiteFileContainerNames.All"/>. A host may register other
/// blob containers for other modules; a Site editor's permissions say nothing about those, so naming one
/// here is refused rather than served.
/// </summary>
public class FileContainerNotAvailableException : BusinessException
{
    public FileContainerNotAvailableException(string? containerName)
        : base(SiteErrorCodes.FileContainerNotAvailable)
    {
        WithData("ContainerName", containerName ?? string.Empty);
    }

    public static void ThrowIfNotSiteContainer(string? containerName)
    {
        if (!SiteFileContainerNames.Contains(containerName))
        {
            throw new FileContainerNotAvailableException(containerName);
        }
    }
}
