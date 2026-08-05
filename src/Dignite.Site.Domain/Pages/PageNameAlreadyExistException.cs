using Volo.Abp;

namespace Dignite.Site.Pages;

/// <summary>
/// A page's name is its stable handle for MCP tools and templates, so it has to be unique per tenant.
/// </summary>
public class PageNameAlreadyExistException : BusinessException
{
    public PageNameAlreadyExistException(string name)
        : base(SiteErrorCodes.PageNameAlreadyExists)
    {
        WithData("Name", name);
    }
}
