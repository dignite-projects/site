using Volo.Abp;

namespace Dignite.Site.Pages;

/// <summary>
/// A route was given that contains a <c>{...}</c> placeholder <see cref="PageRoute"/> does not
/// recognize - a route with no placeholder at all is legitimate (总体设计 §3.3).
/// </summary>
public class InvalidPageRouteException : BusinessException
{
    public InvalidPageRouteException(string route)
        : base(SiteErrorCodes.InvalidPageRoute)
    {
        WithData("Route", route);
    }
}
