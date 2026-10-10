using Volo.Abp;

namespace Dignite.Site.Directories;

/// <summary>Moving a directory under itself or one of its descendants would cut it off into a cycle.</summary>
public class DirectoryCannotMoveIntoItselfException : BusinessException
{
    public DirectoryCannotMoveIntoItselfException()
        : base(SiteErrorCodes.DirectoryCannotMoveIntoItself)
    {
    }
}
