using Volo.Abp;

namespace Dignite.Site.Templates;

/// <summary>
/// The layout template never calls <c>site_head</c>. Every page renders inside the layout, so one that
/// leaves the call out would drop the SEO head tags (title, description, canonical, hreflang) from the
/// whole site - refused at save time rather than discovered in search results.
/// </summary>
public class SiteTemplateMissingSiteHeadException : BusinessException
{
    public SiteTemplateMissingSiteHeadException(string name)
        : base(SiteErrorCodes.SiteTemplateMissingSiteHead)
    {
        WithData("Name", name);
    }
}
