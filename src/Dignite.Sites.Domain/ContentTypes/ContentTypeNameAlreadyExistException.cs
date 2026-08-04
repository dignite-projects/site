using Volo.Abp;

namespace Dignite.Sites.ContentTypes;

/// <summary>
/// A content type's name is unique within its page - not tenant-wide, since types do not cross pages.
/// </summary>
public class ContentTypeNameAlreadyExistException : BusinessException
{
    public ContentTypeNameAlreadyExistException(string name)
        : base(SitesErrorCodes.ContentTypeNameAlreadyExists)
    {
        WithData("Name", name);
    }
}
