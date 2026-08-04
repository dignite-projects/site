using Volo.Abp;

namespace Dignite.Sites.Pages;

/// <summary>
/// Two pages cannot share a route: the route is how a request is resolved to a page, so a duplicate
/// makes the resolution ambiguous.
/// </summary>
public class PageRouteAlreadyExistException : BusinessException
{
    public PageRouteAlreadyExistException(string route)
        : base(SitesErrorCodes.PageRouteAlreadyExists)
    {
        WithData("Route", route);
    }
}
