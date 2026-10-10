using Volo.Abp;

namespace Dignite.Site.Directories;

/// <summary>
/// A directory that does not exist - or is another user's, another tenant's or in another container,
/// which is reported the same way so the answer reveals nothing about directories the caller cannot see.
/// </summary>
public class DirectoryNotFoundBusinessException : BusinessException
{
    public DirectoryNotFoundBusinessException()
        : base(SiteErrorCodes.DirectoryNotFound)
    {
    }
}
