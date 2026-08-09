using Volo.Abp;

namespace Dignite.Site.Contents;

/// <summary>
/// A non-empty slug was given for a content whose page's route has neither <c>{slug}</c> nor
/// <c>{slug?}</c> - such a page has nothing beneath it but its own address (总体设计 §3.3), so a slug
/// there could never be reached by any request path.
/// </summary>
public class ContentSlugNotAllowedException : BusinessException
{
    public ContentSlugNotAllowedException(string route, string slug)
        : base(SiteErrorCodes.ContentSlugNotAllowed)
    {
        WithData("Route", route);
        WithData("Slug", slug);
    }
}
