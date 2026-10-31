namespace Dignite.Site.Templates;

/// <summary>What <see cref="ISiteTemplatePreviewLinkIssuer"/> signs: a version of a template, and the page to open it on.</summary>
public class SiteTemplatePreviewLinkRequest
{
    /// <summary>
    /// The template's name as stored (normalized, <c>SiteTemplate.NormalizeName</c>): a page template, the
    /// <c>layout</c>, a <c>partials/</c> or an <c>assets/</c> name - each is substituted where the site would use it.
    /// </summary>
    public string TemplateName { get; set; } = default!;

    /// <summary>The number of the version the site renders in place of the published one.</summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// The site path to open, as a visitor would request it: <c>/about</c>, <c>/ja/blog/hello</c>, a language
    /// prefix included. Null or empty is the home page.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// The site's origin, without a trailing slash (<c>https://acme.com</c>, or <c>https://acme.com/site</c>
    /// under a path), when the caller knows it - the tenant's primary domain. Null lets the issuer fall back
    /// to the request it is called from.
    /// </summary>
    public string? BaseUrl { get; set; }
}
