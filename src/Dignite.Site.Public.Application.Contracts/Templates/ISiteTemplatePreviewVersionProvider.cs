using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Templates;

namespace Dignite.Site.Public.Templates;

/// <summary>
/// Any saved version of a template - a draft, the published one or an older one - for the site's draft mode: a
/// visitor holding a valid preview ticket sees the site rendered with the version the ticket names.
/// <para>
/// An in-process hook, not an application service, on purpose: <see cref="ISiteTemplatePublicAppService"/> only
/// ever answers with published versions, and that is what keeps a draft out of reach of an anonymous visitor. This
/// interface gets no controller and no client proxy, so a draft is never reachable over HTTP; only the rendering
/// side asks for one, and only after it has verified a ticket. A host that renders the site against a remote
/// Public API has no implementation, and draft mode is then simply off.
/// </para>
/// </summary>
public interface ISiteTemplatePreviewVersionProvider
{
    /// <summary>
    /// The version <paramref name="versionNumber"/> of the current tenant's template called
    /// <paramref name="name"/> (normalized like any template name), or null when there is no such template or
    /// version.
    /// </summary>
    Task<SiteTemplateVersionDto?> FindVersionAsync(string name, int versionNumber, CancellationToken cancellationToken = default);
}
