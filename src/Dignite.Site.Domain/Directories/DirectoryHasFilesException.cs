using Volo.Abp;

namespace Dignite.Site.Directories;

/// <summary>Only an empty directory can be deleted: this one still has files.</summary>
public class DirectoryHasFilesException : BusinessException
{
    public DirectoryHasFilesException()
        : base(SiteErrorCodes.DirectoryHasFiles)
    {
    }
}
