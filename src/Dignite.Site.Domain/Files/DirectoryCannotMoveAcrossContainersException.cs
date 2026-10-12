using Volo.Abp;

namespace Dignite.Site.Files;

/// <summary>A directory and its parent must be in the same file container.</summary>
public class DirectoryCannotMoveAcrossContainersException : BusinessException
{
    public DirectoryCannotMoveAcrossContainersException()
        : base(SiteErrorCodes.DirectoryCannotMoveAcrossContainers)
    {
    }
}
