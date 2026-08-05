using Volo.Abp;

namespace Dignite.Sites.Seo;

/// <summary>
/// No absolute base URL could be determined, so no absolute URL can be emitted - and sitemap, feed and
/// canonical URLs are required to be absolute.
/// <para>
/// Practically unreachable from an HTTP request, where the web layer's resolver falls back to the
/// request's own scheme and host. It exists for callers that have no request to fall back to - a
/// background job, a console tool - where guessing a domain would be worse than failing.
/// </para>
/// </summary>
public class PrimaryDomainNotConfiguredException : BusinessException
{
    public PrimaryDomainNotConfiguredException()
        : base(SitesErrorCodes.PrimaryDomainNotConfigured)
    {
    }
}
