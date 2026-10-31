using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Site.Templates;

/// <summary>
/// Signs the link that opens the live site in a browser with one saved template version in place of the
/// published one - the "preview in a new window" of the administration side. The link carries a short-lived
/// token; the site exchanges it for a preview cookie and renders every page the visitor then opens with the
/// version the token names, with a banner and <c>noindex</c>, until the preview is exited or expires.
/// <para>
/// Declared here for the same reason as <see cref="ISiteTemplatePreviewRenderer"/>: the caller is
/// <c>Dignite.Site.Admin.Application</c>, the implementation is in <c>Dignite.Site.Public.Web</c>, which signs
/// and verifies the token with the application's Data Protection keys - the same keys, since it is the site
/// that honors the link. An in-process hook, not an application service: no controller, no client proxy. An
/// application without <c>Dignite.Site.Public.Web</c> has no implementation, and a link there fails with
/// <see cref="SiteErrorCodes.SiteTemplatePreviewLinkUnavailable"/>.
/// </para>
/// </summary>
public interface ISiteTemplatePreviewLinkIssuer
{
    /// <summary>
    /// The absolute address of <see cref="SiteTemplatePreviewLinkRequest.Path"/> on the site, carrying a token
    /// for the version the request names, valid for a few minutes.
    /// </summary>
    Task<SiteTemplatePreviewLinkDto> IssueAsync(SiteTemplatePreviewLinkRequest request, CancellationToken cancellationToken = default);
}
