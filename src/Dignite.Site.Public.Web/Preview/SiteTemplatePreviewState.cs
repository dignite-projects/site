using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Localization;
using Dignite.Site.Public.Templates;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// One request's answer to "is this visitor in draft mode, and with what": the preview cookie, read once and
/// verified (<see cref="SiteTemplatePreviewTokenProtector"/>), honored only on the tenant it was issued for.
/// Everything that renders the site - <see cref="Routing.SiteRenderController"/> and
/// <see cref="Assets.SiteAssetController"/> - asks here, so a draft is never rendered without a valid ticket.
/// <para>
/// The version is read through <see cref="ISiteTemplatePreviewVersionProvider"/>, resolved only when a ticket is
/// there and optional: a host rendering the site against a remote Public API has none, and its visitors then see
/// the published site even with a valid cookie. Read at most once per request.
/// </para>
/// </summary>
public class SiteTemplatePreviewState : IScopedDependency
{
    private bool _cookieRead;
    private SiteTemplatePreviewTicket? _cookieTicket;
    private SiteTemplatePreviewTicket? _activatedTicket;
    private SiteTemplatePreviewTicket? _versionTicket;
    private SiteTemplateVersionDto? _version;

    protected SiteTemplatePreviewTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }
    protected IStringLocalizer<SiteResource> Localizer { get; }
    protected IUrlHelperFactory UrlHelperFactory { get; }
    protected IServiceProvider ServiceProvider { get; }

    public SiteTemplatePreviewState(
        SiteTemplatePreviewTokenProtector protector,
        ICurrentTenant currentTenant,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<SiteResource> localizer,
        IUrlHelperFactory urlHelperFactory,
        IServiceProvider serviceProvider)
    {
        Protector = protector;
        CurrentTenant = currentTenant;
        HttpContextAccessor = httpContextAccessor;
        Localizer = localizer;
        UrlHelperFactory = urlHelperFactory;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// The ticket this request previews with, or null: no cookie, a cookie that does not verify (tampered,
    /// expired, other keys), or one issued for another tenant than the current one - checked on every read, so a
    /// tenant switched to during the request never sees another tenant's ticket.
    /// </summary>
    public virtual SiteTemplatePreviewTicket? Ticket
    {
        get
        {
            var ticket = _activatedTicket ?? ReadCookie();
            return ticket != null && ticket.TenantId == CurrentTenant.Id ? ticket : null;
        }
    }

    /// <summary>
    /// Previews with <paramref name="ticket"/> for the rest of this request, as if the cookie had carried it - for a
    /// caller that has just verified a token itself. Still honored only on the ticket's tenant.
    /// </summary>
    public virtual void Activate(SiteTemplatePreviewTicket ticket)
    {
        Check.NotNull(ticket, nameof(ticket));

        _activatedTicket = ticket;
    }

    /// <summary>
    /// The version <see cref="Ticket"/> names, or null without a ticket, without a
    /// <see cref="ISiteTemplatePreviewVersionProvider"/>, or when the version no longer exists. Looked up once per
    /// request.
    /// </summary>
    public virtual async Task<SiteTemplateVersionDto?> GetOverrideVersionAsync(CancellationToken cancellationToken = default)
    {
        var ticket = Ticket;
        if (ticket == null)
        {
            return null;
        }

        if (ReferenceEquals(ticket, _versionTicket))
        {
            return _version;
        }

        var provider = ServiceProvider.GetService<ISiteTemplatePreviewVersionProvider>();
        _version = provider == null
            ? null
            : await provider.FindVersionAsync(ticket.TemplateName, ticket.VersionNumber, cancellationToken);
        _versionTicket = ticket;

        return _version;
    }

    /// <summary>
    /// <paramref name="published"/> - the page's published render set - with the ticket's version in its slot
    /// (<see cref="SiteTemplatePreviewOverride.Apply"/>), as a new DTO; <paramref name="published"/> itself, untouched,
    /// when there is nothing to preview.
    /// </summary>
    /// <param name="pageTemplateName">The name of the page's own template (<c>Page.Template</c>, resolved).</param>
    /// <param name="published">What <see cref="ISiteTemplatePublicAppService.GetRenderSetAsync"/> returned for it.</param>
    public virtual async Task<SiteTemplateRenderSetDto> ApplyAsync(string pageTemplateName, SiteTemplateRenderSetDto published)
    {
        Check.NotNull(pageTemplateName, nameof(pageTemplateName));
        Check.NotNull(published, nameof(published));

        var ticket = Ticket;
        if (ticket == null)
        {
            return published;
        }

        var version = await GetOverrideVersionAsync();
        return version == null
            ? published
            : SiteTemplatePreviewOverride.Apply(published, pageTemplateName, ticket.TemplateName, version);
    }

    /// <summary>See <see cref="SiteTemplatePreviewHttp.ApplyHeaders"/>.</summary>
    public virtual void ApplyResponseHeaders(HttpResponse response)
    {
        SiteTemplatePreviewHttp.ApplyHeaders(response);
    }

    /// <summary>
    /// The banner for the page <paramref name="actionContext"/> answers (<see cref="SiteTemplatePreviewBanner"/>), or
    /// null without a ticket. Its exit link goes back to this very address - base path, path and query - once the
    /// cookie is gone. In the request's UI culture: the banner speaks to the author, not to the page's visitors.
    /// </summary>
    public virtual string? GetBannerHtml(ActionContext actionContext)
    {
        Check.NotNull(actionContext, nameof(actionContext));

        var ticket = Ticket;
        if (ticket == null)
        {
            return null;
        }

        var request = actionContext.HttpContext.Request;
        var exitUrl = UrlHelperFactory.GetUrlHelper(actionContext).Content("~/" + SiteTemplatePreviewConsts.ExitPath)
                      + "?returnUrl=" + Uri.EscapeDataString(request.GetEncodedPathAndQuery());

        return SiteTemplatePreviewBanner.Build(
            Localizer["TemplatePreviewBanner", ticket.TemplateName, ticket.VersionNumber],
            Localizer["ExitTemplatePreview"],
            exitUrl);
    }

    private SiteTemplatePreviewTicket? ReadCookie()
    {
        if (!_cookieRead)
        {
            _cookieRead = true;

            var value = HttpContextAccessor.HttpContext?.Request.Cookies[SiteTemplatePreviewConsts.CookieName];
            _cookieTicket = Protector.TryUnprotect(value, out var ticket) ? ticket : null;
        }

        return _cookieTicket;
    }
}
