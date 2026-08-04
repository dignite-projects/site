using Volo.Abp;

namespace Dignite.Sites.Pages;

/// <summary>
/// A page's name is its stable handle for MCP tools and templates, so it has to be unique per tenant.
/// </summary>
public class PageNameAlreadyExistException : BusinessException
{
    public PageNameAlreadyExistException(string name)
        : base(SitesErrorCodes.PageNameAlreadyExists)
    {
        WithData("Name", name);
    }
}
