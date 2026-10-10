using Volo.Abp;

namespace Dignite.Site.Directories;

/// <summary>Sibling directories of one owner need distinct names.</summary>
public class DirectoryNameAlreadyExistException : BusinessException
{
    public DirectoryNameAlreadyExistException(string directoryName)
        : base(SiteErrorCodes.DirectoryNameAlreadyExists)
    {
        WithData("DirectoryName", directoryName);
    }
}
