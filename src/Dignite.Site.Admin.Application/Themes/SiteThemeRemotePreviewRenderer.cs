using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// Renders a preview in the application that serves the site, for an administration API hosted without
/// <c>Dignite.Site.Public.Web</c>: it sends the snapshot to the site's <c>POST /site-preview/render</c>
/// (<see cref="SiteThemePreviewConsts.RenderPath"/>), which renders it with the site's own renderer, services and views.
/// The site's address is the remote service <see cref="SiteThemePreviewConsts.RendererRemoteServiceName"/>
/// (<c>RemoteServices:SitePublicWeb:BaseUrl</c>) - the application's internal address will do: what a page writes is
/// relative, or built on the tenant's <c>Site.PrimaryDomain</c>, not on the address it was asked at.
/// <para>
/// Used only when the application has no renderer of its own and that remote service is configured - see
/// <see cref="SiteThemePreviewRendererProvider"/>. Each call carries a <see cref="SitePreviewServiceTokenProtector"/>
/// token naming the current tenant, so the two applications must share one Data Protection key ring.
/// </para>
/// <para>
/// A business exception of the site's renderer comes back as data and is raised here again, code and data alike, so
/// a caller cannot tell a remote render from a local one. Any other failure - the site unreachable, an error status -
/// is raised as an <see cref="AbpException"/>: the application's failure, not the theme's.
/// </para>
/// </summary>
[ExposeServices(typeof(SiteThemeRemotePreviewRenderer))]
public class SiteThemeRemotePreviewRenderer : ISiteThemePreviewRenderer, ITransientDependency
{
    /// <summary>The named <see cref="HttpClient"/> the calls are made with.</summary>
    public const string HttpClientName = "Dignite.Site.ThemePreview";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected IHttpClientFactory HttpClientFactory { get; }
    protected IOptions<AbpRemoteServiceOptions> RemoteServiceOptions { get; }
    protected SitePreviewServiceTokenProtector Protector { get; }
    protected ICurrentTenant CurrentTenant { get; }

    public SiteThemeRemotePreviewRenderer(
        IHttpClientFactory httpClientFactory,
        IOptions<AbpRemoteServiceOptions> remoteServiceOptions,
        SitePreviewServiceTokenProtector protector,
        ICurrentTenant currentTenant)
    {
        HttpClientFactory = httpClientFactory;
        RemoteServiceOptions = remoteServiceOptions;
        Protector = protector;
        CurrentTenant = currentTenant;
    }

    /// <summary>Whether the site's address is configured - without it there is nothing to render with.</summary>
    public virtual bool IsConfigured => GetBaseUrl() != null;

    public virtual async Task<SiteThemePreviewDto> RenderAsync(
        SiteThemePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));
        Check.NotNull(request.Snapshot, nameof(request.Snapshot));

        var baseUrl = GetBaseUrl() ?? throw new BusinessException(SiteErrorCodes.SiteTemplatePreviewUnavailable);
        var address = baseUrl + "/" + SiteThemePreviewConsts.RenderPath;

        var input = new SiteThemePreviewRenderInput
        {
            Token = Protector.Protect(new SitePreviewTicket(CurrentTenant.Id, request.Snapshot.ReleaseId)),
            Path = request.Path,
            Snapshot = request.Snapshot
        };

        var client = HttpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(address, input, JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AbpException(
                $"The site at '{address}' answered the preview render with {(int)response.StatusCode} " +
                $"{response.ReasonPhrase}. Check RemoteServices:{SiteThemePreviewConsts.RendererRemoteServiceName}:BaseUrl " +
                "and that both applications share one Data Protection key ring.");
        }

        var result = await response.Content.ReadFromJsonAsync<SiteThemePreviewRenderResult>(JsonOptions, cancellationToken);

        if (result?.Error != null)
        {
            throw ToException(result.Error);
        }

        return result?.Preview
               ?? throw new AbpException($"The site at '{address}' answered the preview render with neither a page nor an error.");
    }

    /// <summary>The address of the site's application, without a trailing slash, or null when it is not configured.</summary>
    protected virtual string? GetBaseUrl()
    {
        return RemoteServiceOptions.Value.RemoteServices.TryGetValue(SiteThemePreviewConsts.RendererRemoteServiceName, out var configuration)
               && !string.IsNullOrWhiteSpace(configuration?.BaseUrl)
            ? configuration.BaseUrl.Trim().TrimEnd('/')
            : null;
    }

    /// <summary>
    /// The site's business exception, raised again: its code, and its data, with <c>Line</c> and <c>Column</c> as numbers
    /// again, as the renderer sets them.
    /// </summary>
    protected virtual Exception ToException(SiteThemePreviewRenderError error)
    {
        if (string.IsNullOrEmpty(error.Code))
        {
            return new AbpException("The site's renderer failed without saying why.");
        }

        var exception = new BusinessException(error.Code);
        foreach (var (key, value) in error.Data)
        {
            if (key is "Line" or "Column" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                exception.WithData(key, number);
            }
            else
            {
                exception.WithData(key, value!);
            }
        }

        return exception;
    }
}
