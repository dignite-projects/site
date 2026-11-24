using Volo.Abp;

namespace Dignite.Site.ContentTypes;

/// <summary>A content type is deleted only when no content uses it: move or delete those contents first.</summary>
public class ContentTypeHasContentsException : BusinessException
{
    public ContentTypeHasContentsException(string name)
        : base(SiteErrorCodes.ContentTypeHasContents)
    {
        WithData("Name", name);
    }
}
