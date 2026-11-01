using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// Issues the links of the site's preview: the requested path on the site, with a token
/// (<see cref="SitePreviewConsts.QueryKey"/>) that <see cref="SitePreviewResourceFilter"/> exchanges for the preview
/// cookie. The token names the current tenant and the theme - the working copy or a release - and expires after
/// <see cref="SitePreviewOptions.TokenLifetime"/>; it carries no permission of its own beyond showing that theme on that
/// tenant's site, which is why the administration side - not this class - decides who may ask for one.
/// <para>
/// Here and not on the administration side because it is the site that has to read the token back: issuer and verifier
/// share <see cref="SitePreviewTokenProtector"/> and thus the application's Data Protection keys.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteThemePreviewLinkIssuer), typeof(SiteThemePreviewLinkIssuer))]
public class SiteThemePreviewLinkIssuer : ISiteThemePreviewLinkIssuer, ITransientDependency
{
    protected SitePreviewTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }
    protected IOptions<SitePreviewOptions> Options { get; }

    public SiteThemePreviewLinkIssuer(
        SitePreviewTokenProtector protector,
        ICurrentTenant currentTenant,
        IHttpContextAccessor httpContextAccessor,
        IOptions<SitePreviewOptions> options)
    {
        Protector = protector;
        CurrentTenant = currentTenant;
        HttpContextAccessor = httpContextAccessor;
        Options = options;
    }

    public virtual Task<SiteThemePreviewLinkDto> IssueAsync(
        SiteThemePreviewLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));

        var ticket = new SitePreviewTicket(CurrentTenant.Id, request.ReleaseId);
        var token = Protector.Protect(ticket, Options.Value.TokenLifetime, out var expiresAt);

        var path = NormalizePath(request.Path);
        var separator = path.Contains('?') ? "&" : "?";

        return Task.FromResult(new SiteThemePreviewLinkDto
        {
            Url = GetBaseUrl(request) + path + separator + SitePreviewConsts.QueryKey + "=" + Uri.EscapeDataString(token),
            Path = path,
            ReleaseId = request.ReleaseId,
            ExpiresAt = expiresAt.UtcDateTime
        });
    }

    /// <summary>
    /// <see cref="SiteThemePreviewLinkRequest.BaseUrl"/> without a trailing slash, else the origin and base path of the
    /// request this is called from - the administration API's, which is the site's own in an application that serves
    /// both. A host whose site lives elsewhere overrides this.
    /// </summary>
    /// <exception cref="AbpException">Neither is available.</exception>
    protected virtual string GetBaseUrl(SiteThemePreviewLinkRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return request.BaseUrl.Trim().TrimEnd('/');
        }

        var httpRequest = HttpContextAccessor.HttpContext?.Request
                          ?? throw new AbpException(
                              "A preview link needs the site's address: none was given " +
                              $"({nameof(SiteThemePreviewLinkRequest)}.{nameof(SiteThemePreviewLinkRequest.BaseUrl)}), " +
                              "and there is no HTTP request to take it from.");

        return httpRequest.Scheme + "://" + httpRequest.Host.ToUriComponent() + httpRequest.PathBase.ToUriComponent();
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
