using Volo.Abp;

namespace Dignite.Site.Directories;

/// <summary>A directory and its parent must be in the same file container.</summary>
public class DirectoryCannotMoveAcrossContainersException : BusinessException
{
    public DirectoryCannotMoveAcrossContainersException()
        : base(SiteErrorCodes.DirectoryCannotMoveAcrossContainers)
    {
    }
}
