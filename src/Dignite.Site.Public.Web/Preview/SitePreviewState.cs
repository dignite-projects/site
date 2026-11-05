using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Localization;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
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
/// One request's answer to "is this visitor previewing, and what": the preview cookie, read once and verified
/// (<see cref="SitePreviewTokenProtector"/>), honored only on the tenant it was issued for. Everything that renders the
/// site - <see cref="Routing.SiteRenderController"/> and <see cref="Assets.SiteAssetController"/> - asks here, so the
/// working copy is never rendered without a valid ticket.
/// <para>
/// The theme is read through <see cref="ISiteThemePreviewSource"/>, resolved only when a ticket is there and optional:
/// an application without one serves the live site even with a valid cookie. A site rendered against a remote Public
/// API has one through <c>Dignite.Site.Public.HttpApi.Client</c>, which reads the theme over the Public integration
/// service. Read at most once per request, and again on the next one: a preview of the working copy shows each
/// save on the next page view.
/// </para>
/// </summary>
public class SitePreviewState : IScopedDependency
{
    private bool _cookieRead;
    private SitePreviewTicket? _cookieTicket;
    private bool _renderSetRead;
    private SiteThemeRenderSet? _renderSet;

    protected SitePreviewTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }
    protected IStringLocalizer<SiteResource> Localizer { get; }
    protected IUrlHelperFactory UrlHelperFactory { get; }
    protected IServiceProvider ServiceProvider { get; }

    public SitePreviewState(
        SitePreviewTokenProtector protector,
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
    /// The ticket this request previews with, or null: no cookie, a cookie that does not verify (tampered, expired,
    /// other keys), or one issued for another tenant than the current one - checked on every read, so a tenant switched
    /// to during the request never sees another tenant's ticket.
    /// </summary>
    public virtual SitePreviewTicket? Ticket
    {
        get
        {
            var ticket = ReadCookie();
            return ticket != null && ticket.TenantId == CurrentTenant.Id ? ticket : null;
        }
    }

    /// <summary>
    /// Whether this request previews and can: a <see cref="Ticket"/>, and an <see cref="ISiteThemePreviewSource"/> to read
    /// the theme from. Without a source the site is served live, static files included.
    /// </summary>
    public virtual bool IsAvailable => Ticket != null && ServiceProvider.GetService<ISiteThemePreviewSource>() != null;

    /// <summary>
    /// The theme <see cref="Ticket"/> names, ready to render: null without a ticket, without an
    /// <see cref="ISiteThemePreviewSource"/>, or when the tenant has no theme or no longer such a release - the page then
    /// renders with the live release.
    /// </summary>
    public virtual async Task<SiteThemeRenderSet?> GetRenderSetAsync(CancellationToken cancellationToken = default)
    {
        var ticket = Ticket;
        if (ticket == null)
        {
            return null;
        }

        if (!_renderSetRead)
        {
            var source = ServiceProvider.GetService<ISiteThemePreviewSource>();
            var snapshot = source == null ? null : await source.FindSnapshotAsync(ticket.ReleaseId, cancellationToken);
            _renderSet = snapshot == null ? null : new SiteThemeRenderSet(snapshot);
            _renderSetRead = true;
        }

        return _renderSet;
    }

    /// <summary>
    /// A static file of the theme <see cref="Ticket"/> names, or null without a ticket, without an
    /// <see cref="ISiteThemePreviewSource"/>, or when that theme has no static file at <paramref name="path"/>.
    /// </summary>
    public virtual async Task<SiteThemeAssetDto?> FindAssetAsync(string path, CancellationToken cancellationToken = default)
    {
        var ticket = Ticket;
        if (ticket == null)
        {
            return null;
        }

        var source = ServiceProvider.GetService<ISiteThemePreviewSource>();
        return source == null ? null : await source.FindAssetAsync(ticket.ReleaseId, path, cancellationToken);
    }

    /// <summary>See <see cref="SitePreviewHttp.ApplyHeaders"/>.</summary>
    public virtual void ApplyResponseHeaders(HttpResponse response)
    {
        SitePreviewHttp.ApplyHeaders(response);
    }

    /// <summary>
    /// The banner for the page <paramref name="actionContext"/> answers (<see cref="SitePreviewBanner"/>), or null without
    /// a ticket. It says what is shown - the working copy, a release, or the live release when the previewed theme is
    /// gone - and its exit link goes back to this very address once the cookie is gone. In the request's UI culture: the
    /// banner speaks to the author, not to the page's visitors. Call it after <see cref="GetRenderSetAsync"/>.
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
        var exitUrl = UrlHelperFactory.GetUrlHelper(actionContext).Content("~/" + SitePreviewConsts.ExitPath)
                      + "?returnUrl=" + Uri.EscapeDataString(request.GetEncodedPathAndQuery());

        return SitePreviewBanner.Build(GetBannerText(ticket), Localizer["ExitPreview"], exitUrl);
    }

    protected virtual string GetBannerText(SitePreviewTicket ticket)
    {
        if (_renderSet == null)
        {
            return Localizer["PreviewBanner:Unavailable"];
        }

        return _renderSet.Snapshot.ReleaseNumber is { } number
            ? Localizer["PreviewBanner:Release", number]
            : Localizer["PreviewBanner:WorkingCopy"];
    }

    private SitePreviewTicket? ReadCookie()
    {
        if (!_cookieRead)
        {
            _cookieRead = true;

            var value = HttpContextAccessor.HttpContext?.Request.Cookies[SitePreviewConsts.CookieName];
            _cookieTicket = Protector.TryUnprotect(value, out var ticket) ? ticket : null;
        }

        return _cookieTicket;
    }
}
