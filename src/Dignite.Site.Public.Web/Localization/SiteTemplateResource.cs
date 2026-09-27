using Volo.Abp.Localization;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The texts a site's own templates show - option names, labels, archive headings - written by whoever
/// writes the templates, in JSON files next to them (GitHub issue #73). One fixed resource for every
/// tenant: what differs per tenant is the files it reads, not the resource a template injects, so one
/// template can serve many tenants.
/// <para>
/// Read with <c>IStringLocalizer&lt;SiteTemplateResource&gt;</c>, <c>IHtmlLocalizer&lt;SiteTemplateResource&gt;</c>,
/// or <c>IViewLocalizer</c> inside a <c>/Sites/</c> template (<see cref="SiteViewLocalizer"/>). Where the
/// texts come from and in which order is <see cref="SiteTemplateLocalizationContributor"/>'s job.
/// </para>
/// <para>
/// Registered only by <see cref="SitePublicWebModule"/>, and must stay that way: in a tiered front end,
/// ABP's <c>RemoteLocalizationContributor</c> is asked first for every resource, and it would answer with the
/// API's texts if an API-side module ever registered a resource of this name.
/// </para>
/// </summary>
[LocalizationResourceName(ResourceName)]
public class SiteTemplateResource
{
    public const string ResourceName = "SiteTemplate";
}
