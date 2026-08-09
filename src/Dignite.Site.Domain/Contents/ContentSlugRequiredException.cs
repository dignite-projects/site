using Volo.Abp;

namespace Dignite.Site.Contents;

/// <summary>
/// An empty slug was given for a content whose page's route has a mandatory <c>{slug}</c> - unlike
/// <c>{slug?}</c>, that route never resolves to a default content at the page's own address, so every
/// content beneath it needs a slug of its own (总体设计 §3.3).
/// </summary>
public class ContentSlugRequiredException : BusinessException
{
    public ContentSlugRequiredException(string route)
        : base(SiteErrorCodes.ContentSlugRequired)
    {
        WithData("Route", route);
    }
}
