namespace Dignite.Site.Templates;

/// <summary>
/// What a template's name says it is - see <see cref="SiteTemplateNames.GetKind"/>. Decides how a template
/// is rendered (a page, the layout around every page, a fragment an <c>include</c> pulls in, a text file an
/// <c>asset_url</c> points at) and therefore what "using it" means.
/// </summary>
public enum SiteTemplateKind
{
    /// <summary>A page template: rendered for every page whose <c>Template</c> is its name.</summary>
    Page = 0,

    /// <summary>The template named <see cref="SiteTemplateConsts.LayoutName"/>, which wraps every page.</summary>
    Layout = 1,

    /// <summary>A template under <see cref="SiteTemplateConsts.PartialsPrefix"/>, pulled in with <c>include</c>.</summary>
    Partial = 2,

    /// <summary>A template under <see cref="SiteTemplateConsts.AssetsPrefix"/>, served as it is at <c>/site-assets/</c>.</summary>
    Asset = 3
}
