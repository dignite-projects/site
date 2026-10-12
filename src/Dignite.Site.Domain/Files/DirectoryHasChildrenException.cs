using Volo.Abp;

namespace Dignite.Site.Files;

/// <summary>Only an empty directory can be deleted: this one still has subdirectories.</summary>
public class DirectoryHasChildrenException : BusinessException
{
    public DirectoryHasChildrenException()
        : base(SiteErrorCodes.DirectoryHasChildren)
    {
    }
}
