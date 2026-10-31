using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// Issues the links of the site's draft mode: the requested path on the site, with a token
/// (<see cref="SiteTemplatePreviewConsts.QueryKey"/>) that <see cref="SiteTemplatePreviewResourceFilter"/> exchanges
/// for the preview cookie. The token names the current tenant, the template and the version, and expires after
/// <see cref="SiteTemplatePreviewOptions.TokenLifetime"/>; it carries no permission of its own beyond showing that
/// version on that tenant's site, which is why the administration side - not this class - decides who may ask for
/// one.
/// <para>
/// Here and not on the administration side because it is the site that has to read the token back: issuer and
/// verifier share <see cref="SiteTemplatePreviewTokenProtector"/> and thus the application's Data Protection keys.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteTemplatePreviewLinkIssuer), typeof(SiteTemplatePreviewLinkIssuer))]
public class SiteTemplatePreviewLinkIssuer : ISiteTemplatePreviewLinkIssuer, ITransientDependency
{
    protected SiteTemplatePreviewTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }
    protected IOptions<SiteTemplatePreviewOptions> Options { get; }

    public SiteTemplatePreviewLinkIssuer(
        SiteTemplatePreviewTokenProtector protector,
        ICurrentTenant currentTenant,
        IHttpContextAccessor httpContextAccessor,
        IOptions<SiteTemplatePreviewOptions> options)
    {
        Protector = protector;
        CurrentTenant = currentTenant;
        HttpContextAccessor = httpContextAccessor;
        Options = options;
    }

    public virtual Task<SiteTemplatePreviewLinkDto> IssueAsync(
        SiteTemplatePreviewLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));
        Check.NotNullOrWhiteSpace(request.TemplateName, nameof(request.TemplateName));
        if (request.VersionNumber <= 0)
        {
            // Versions are numbered from 1; a ticket for any other number would never verify.
            throw new ArgumentException(
                $"{nameof(request.VersionNumber)} must be positive; it is {request.VersionNumber}.", nameof(request));
        }

        var ticket = new SiteTemplatePreviewTicket(CurrentTenant.Id, request.TemplateName, request.VersionNumber);
        var token = Protector.Protect(ticket, Options.Value.TokenLifetime, out var expiresAt);

        var path = NormalizePath(request.Path);
        var separator = path.Contains('?') ? "&" : "?";

        return Task.FromResult(new SiteTemplatePreviewLinkDto
        {
            Url = GetBaseUrl(request) + path + separator + SiteTemplatePreviewConsts.QueryKey + "=" +
                  Uri.EscapeDataString(token),
            Path = path,
            TemplateName = ticket.TemplateName,
            VersionNumber = ticket.VersionNumber,
            ExpiresAt = expiresAt.UtcDateTime
        });
    }

    /// <summary>
    /// <see cref="SiteTemplatePreviewLinkRequest.BaseUrl"/> without a trailing slash, else the origin and base path of
    /// the request this is called from - the administration API's, which is the site's own in an application that
    /// serves both.
    /// </summary>
    /// <exception cref="AbpException">Neither is available.</exception>
    protected virtual string GetBaseUrl(SiteTemplatePreviewLinkRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return request.BaseUrl.Trim().TrimEnd('/');
        }

        var httpRequest = HttpContextAccessor.HttpContext?.Request
                          ?? throw new AbpException(
                              "A template preview link needs the site's address: none was given " +
                              $"({nameof(SiteTemplatePreviewLinkRequest)}.{nameof(SiteTemplatePreviewLinkRequest.BaseUrl)}), " +
                              "and there is no HTTP request to take it from.");

        return httpRequest.Scheme + "://" + httpRequest.Host.ToUriComponent() + httpRequest.PathBase.ToUriComponent();
    }

    /// <summary>Null or empty is the home page; a missing leading slash is added.</summary>
    protected virtual string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        path = path.Trim();
        return path.StartsWith('/') ? path : "/" + path;
    }
}
