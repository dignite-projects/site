using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Seo;
using Dignite.Site.Themes;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// Issues the links of the site's preview: the requested path on the site, with a token
/// (<c>?site-preview=</c>) that the site exchanges for the preview cookie. The token names the current tenant and the
/// theme - the working copy or a release - and expires after <see cref="SitePreviewOptions.TokenLifetime"/>; it carries
/// no permission of its own beyond showing that theme on that tenant's site, which is why the administration side - not
/// this class - decides who may ask for one.
/// <para>
/// The link goes to the tenant's own address, <see cref="ISiteBaseUrlResolver"/> - the <c>Site.PrimaryDomain</c>
/// setting, the address canonical links and the sitemap use - so it works when every tenant has a domain of its own and
/// when the administration API runs in another application than the site. The site reads the token with the same Data
/// Protection keys (<see cref="SitePreviewTokenProtector"/>), which both applications must share.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteThemePreviewLinkIssuer), typeof(SiteThemePreviewLinkIssuer))]
public class SiteThemePreviewLinkIssuer : ISiteThemePreviewLinkIssuer, ITransientDependency
{
    protected SitePreviewTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected ISiteBaseUrlResolver BaseUrlResolver { get; }
    protected IOptions<SitePreviewOptions> Options { get; }

    public SiteThemePreviewLinkIssuer(
        SitePreviewTokenProtector protector,
        ICurrentTenant currentTenant,
        ISiteBaseUrlResolver baseUrlResolver,
        IOptions<SitePreviewOptions> options)
    {
        Protector = protector;
        CurrentTenant = currentTenant;
        BaseUrlResolver = baseUrlResolver;
        Options = options;
    }

    public virtual async Task<SiteThemePreviewLinkDto> IssueAsync(
        SiteThemePreviewLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));

        var baseUrl = await GetBaseUrlAsync(request, cancellationToken);

        var ticket = new SitePreviewTicket(CurrentTenant.Id, request.ReleaseId);
        var token = Protector.Protect(ticket, Options.Value.TokenLifetime, out var expiresAt);

        var path = NormalizePath(request.Path);
        var separator = path.Contains('?') ? "&" : "?";

        return new SiteThemePreviewLinkDto
        {
            Url = baseUrl + path + separator + SiteThemePreviewConsts.QueryKey + "=" + Uri.EscapeDataString(token),
            Path = path,
            ReleaseId = request.ReleaseId,
            ExpiresAt = expiresAt.UtcDateTime
        };
    }

    /// <summary>
    /// <see cref="SiteThemePreviewLinkRequest.BaseUrl"/> without a trailing slash, else the tenant's site address.
    /// </summary>
    /// <exception cref="PrimaryDomainNotConfiguredException">Neither is there.</exception>
    protected virtual async Task<string> GetBaseUrlAsync(SiteThemePreviewLinkRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return request.BaseUrl.Trim().TrimEnd('/');
        }

        var baseUrl = await BaseUrlResolver.GetBaseUrlAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new PrimaryDomainNotConfiguredException();
        }

        return baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Null or empty is the home page; a missing leading slash is added; a path that starts with more than one slash or
    /// a backslash is reduced to one slash, so the link stays on the site.
    /// </summary>
    protected virtual string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return "/" + path.Trim().TrimStart('/', '\\');
    }
}
