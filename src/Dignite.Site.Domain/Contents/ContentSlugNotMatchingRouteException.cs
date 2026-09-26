using Volo.Abp;

namespace Dignite.Site.Contents;

/// <summary>
/// A slug was given that its page's <c>{slug:REGEX}</c> turns down - the router would never resolve a
/// request for it (<see cref="Pages.PageRoute.IsSlugAllowed"/>), so the content would be published at a
/// URL that answers 404.
/// </summary>
public class ContentSlugNotMatchingRouteException : BusinessException
{
    public ContentSlugNotMatchingRouteException(string route, string slug)
        : base(SiteErrorCodes.ContentSlugNotMatchingRoute)
    {
        WithData("Route", route);
        WithData("Slug", slug);
    }
}
