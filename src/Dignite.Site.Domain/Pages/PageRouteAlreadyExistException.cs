using Volo.Abp;

namespace Dignite.Site.Pages;

/// <summary>
/// Two pages cannot share a route: the route is how a request is resolved to a page, so a duplicate
/// makes the resolution ambiguous.
/// </summary>
public class PageRouteAlreadyExistException : BusinessException
{
    public PageRouteAlreadyExistException(string route)
        : base(SiteErrorCodes.PageRouteAlreadyExists)
    {
        WithData("Route", route);
    }
}
