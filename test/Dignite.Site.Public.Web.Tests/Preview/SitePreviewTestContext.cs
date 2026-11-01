using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Localization;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The pieces of the site's preview over one request, without an application around them: an ephemeral Data
/// Protection key ring, a recording <see cref="ISiteThemePreviewSource"/>, and the texts of <see cref="SiteResource"/>
/// the banner and the error page use.
/// </summary>
public class SitePreviewTestContext
{
    public SitePreviewTestContext(HttpContext httpContext, ICurrentTenant currentTenant)
    {
        HttpContext = httpContext;
        CurrentTenant = currentTenant;
        Protector = new SitePreviewTokenProtector(DataProtection);
    }

    public HttpContext HttpContext { get; }

    public ICurrentTenant CurrentTenant { get; }

    public EphemeralDataProtectionProvider DataProtection { get; } = new();

    public SitePreviewTokenProtector Protector { get; }

    public FakePreviewSource Source { get; } = new();

    public FakeSiteLocalizer Localizer { get; } = new();

    public IOptions<SitePreviewOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new SitePreviewOptions());

    /// <summary>False: a host without the in-process source, as when the site renders against a remote Public API.</summary>
    public bool HasSource { get; set; } = true;

    public SitePreviewState CreateState()
    {
        var services = new ServiceCollection();
        if (HasSource)
        {
            services.AddSingleton<ISiteThemePreviewSource>(Source);
        }

        return new SitePreviewState(
            Protector,
            CurrentTenant,
            // Not an HttpContextAccessor: setting one clears the context every other accessor of this flow holds.
            new FixedHttpContextAccessor { HttpContext = HttpContext },
            Localizer,
            new UrlHelperFactory(),
            services.BuildServiceProvider());
    }

    /// <summary>A token as the link issuer and the resource filter write it.</summary>
    public string Token(Guid? releaseId = null, Guid? tenantId = null, TimeSpan? lifetime = null)
    {
        return Protector.Protect(new SitePreviewTicket(tenantId, releaseId), lifetime ?? TimeSpan.FromMinutes(10), out _);
    }

    /// <summary>Sends the preview cookie with the request, as a browser that has been through a preview link does.</summary>
    public void SetCookie(Guid? releaseId = null, Guid? tenantId = null)
    {
        SetCookie(Token(releaseId, tenantId));
    }

    public void SetCookie(string value)
    {
        HttpContext.Request.Headers.Cookie = SitePreviewConsts.CookieName + "=" + value;
    }
}

/// <summary>
/// The working copy (key null) and releases by id, with the reads recorded. A static file's body is in the snapshot;
/// <see cref="FindAssetAsync"/> returns it from there.
/// </summary>
/// <summary>An <see cref="IHttpContextAccessor"/> that holds its context in a field, sharing nothing.</summary>
public class FixedHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}

public class FakePreviewSource : ISiteThemePreviewSource
{
    public SiteThemeSnapshotDto? WorkingCopy { get; set; }

    public Dictionary<Guid, SiteThemeSnapshotDto> Releases { get; } = new();

    public List<Guid?> SnapshotRequests { get; } = new();

    public List<(Guid? ReleaseId, string Path)> AssetRequests { get; } = new();

    public Task<SiteThemeSnapshotDto?> FindSnapshotAsync(Guid? releaseId, CancellationToken cancellationToken = default)
    {
        SnapshotRequests.Add(releaseId);
        return Task.FromResult(Find(releaseId));
    }

    public Task<SiteThemeAssetDto?> FindAssetAsync(Guid? releaseId, string path, CancellationToken cancellationToken = default)
    {
        AssetRequests.Add((releaseId, path));
        var file = Find(releaseId)?.Files.FirstOrDefault(f => f.Path == path && SiteThemePaths.IsAsset(f.Path));
        return Task.FromResult(file == null
            ? null
            : new SiteThemeAssetDto { Path = file.Path, ContentHash = file.ContentHash, Body = file.Body });
    }

    private SiteThemeSnapshotDto? Find(Guid? releaseId)
    {
        return releaseId == null ? WorkingCopy : Releases.GetValueOrDefault(releaseId.Value);
    }
}

public class FakeSiteLocalizer : IStringLocalizer<SiteResource>
{
    public Dictionary<string, string> Texts { get; } = new()
    {
        ["PreviewBanner:WorkingCopy"] = "Previewing the working copy",
        ["PreviewBanner:Release"] = "Previewing release #{0}",
        ["PreviewBanner:Unavailable"] = "Nothing to preview",
        ["ExitPreview"] = "Exit preview",
        ["PreviewRenderFailed"] = "Rendering {0} failed"
    };

    public LocalizedString this[string name] =>
        Texts.TryGetValue(name, out var value)
            ? new LocalizedString(name, value)
            : new LocalizedString(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(CultureInfo.InvariantCulture, this[name].Value, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Texts.Select(pair => new LocalizedString(pair.Key, pair.Value));
}
