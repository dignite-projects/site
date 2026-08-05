namespace Dignite.Site.Seo;

/// <summary>
/// Site-wide facts every JSON-LD Organization/WebSite node needs (总体设计 §5.4, GitHub issue #20).
/// Resolved once per <see cref="HeadMetadataBuilder.BuildAsync"/> call, the same shape as
/// <see cref="Dignite.Site.Seo.SiteUrlContext"/> one level down.
/// </summary>
public class JsonLdSiteData
{
    public JsonLdSiteData(string organizationName, string? organizationLogoUrl, string baseUrl)
    {
        OrganizationName = organizationName;
        OrganizationLogoUrl = organizationLogoUrl;
        BaseUrl = baseUrl;
    }

    /// <summary>
    /// Never blank - resolved through the tenant-aware <c>IBrandingProvider</c>, which already falls back
    /// to the platform default name when a tenant has not set its own (总体设计 §4.1).
    /// </summary>
    public string OrganizationName { get; }

    /// <summary>Null omits <c>logo</c> from the Organization node rather than inventing a placeholder.</summary>
    public string? OrganizationLogoUrl { get; }

    public string BaseUrl { get; }
}
