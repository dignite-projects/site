using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Localization;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Templates;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The pieces of the site's draft mode over one request, without an application around them: an ephemeral Data
/// Protection key ring, a recording <see cref="ISiteTemplatePreviewVersionProvider"/>, and the texts of
/// <see cref="SiteResource"/> the banner and the error page use.
/// </summary>
public class SiteTemplatePreviewTestContext
{
    public SiteTemplatePreviewTestContext(SiteLanguageTestHost host)
        : this(host.HttpContext, host.CurrentTenant)
    {
    }

    public SiteTemplatePreviewTestContext(HttpContext httpContext, ICurrentTenant currentTenant)
    {
        HttpContext = httpContext;
        CurrentTenant = currentTenant;
        Protector = new SiteTemplatePreviewTokenProtector(DataProtection);
    }

    public HttpContext HttpContext { get; }

    public ICurrentTenant CurrentTenant { get; }

    public EphemeralDataProtectionProvider DataProtection { get; } = new();

    public SiteTemplatePreviewTokenProtector Protector { get; }

    public FakePreviewVersionProvider Versions { get; } = new();

    public FakeSiteLocalizer Localizer { get; } = new();

    public IOptions<SiteTemplatePreviewOptions> Options { get; } =
        Microsoft.Extensions.Options.Options.Create(new SiteTemplatePreviewOptions());

    /// <summary>False: a host without the in-process provider, as when the site renders against a remote Public API.</summary>
    public bool HasVersionProvider { get; set; } = true;

    public SiteTemplatePreviewState CreateState()
    {
        var services = new ServiceCollection();
        if (HasVersionProvider)
        {
            services.AddSingleton<ISiteTemplatePreviewVersionProvider>(Versions);
        }

        return new SiteTemplatePreviewState(
            Protector,
            CurrentTenant,
            new HttpContextAccessor { HttpContext = HttpContext },
            Localizer,
            new UrlHelperFactory(),
            services.BuildServiceProvider());
    }

    /// <summary>A token as the link issuer and the resource filter write it.</summary>
    public string Token(string templateName, int versionNumber, Guid? tenantId = null, TimeSpan? lifetime = null)
    {
        return Protector.Protect(
            new SiteTemplatePreviewTicket(tenantId, templateName, versionNumber),
            lifetime ?? TimeSpan.FromMinutes(10),
            out _);
    }

    /// <summary>Sends the preview cookie with the request, as a browser that has been through a preview link does.</summary>
    public void SetCookie(string templateName, int versionNumber, Guid? tenantId = null)
    {
        SetCookie(Token(templateName, versionNumber, tenantId));
    }

    public void SetCookie(string value)
    {
        HttpContext.Request.Headers.Cookie = SiteTemplatePreviewConsts.CookieName + "=" + value;
    }
}

public class FakePreviewVersionProvider : ISiteTemplatePreviewVersionProvider
{
    private readonly Dictionary<(string Name, int Number), SiteTemplateVersionDto> _versions = new();

    public List<(string Name, int Number)> Requests { get; } = new();

    public SiteTemplateVersionDto Add(string name, int number, string body)
    {
        var version = new SiteTemplateVersionDto { Id = Guid.NewGuid(), TemplateId = Guid.NewGuid(), Number = number, Body = body };
        _versions[(name, number)] = version;
        return version;
    }

    public Task<SiteTemplateVersionDto?> FindVersionAsync(string name, int versionNumber, CancellationToken cancellationToken = default)
    {
        Requests.Add((name, versionNumber));
        return Task.FromResult<SiteTemplateVersionDto?>(_versions.GetValueOrDefault((name, versionNumber)));
    }
}

public class FakeSiteLocalizer : IStringLocalizer<SiteResource>
{
    public Dictionary<string, string> Texts { get; } = new()
    {
        // No quotes: the encoder's spelling of an apostrophe is not what these tests are about.
        ["TemplatePreviewBanner"] = "Previewing {0} v{1}",
        ["ExitTemplatePreview"] = "Exit preview",
        ["TemplatePreviewRenderError"] = "The preview could not be rendered:"
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
