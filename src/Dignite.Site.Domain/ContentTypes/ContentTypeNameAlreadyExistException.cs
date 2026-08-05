using Volo.Abp;

namespace Dignite.Site.ContentTypes;

/// <summary>
/// A content type's name is unique within its page - not tenant-wide, since types do not cross pages.
/// </summary>
public class ContentTypeNameAlreadyExistException : BusinessException
{
    public ContentTypeNameAlreadyExistException(string name)
        : base(SiteErrorCodes.ContentTypeNameAlreadyExists)
    {
        WithData("Name", name);
    }
}
