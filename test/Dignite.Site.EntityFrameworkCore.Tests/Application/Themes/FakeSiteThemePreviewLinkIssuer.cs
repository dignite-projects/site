using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// Stands in for the link issuer (<see cref="SiteThemePreviewLinkIssuer"/>, tested on its own), so the app service's tests
/// need no site address: records each request and answers with a recognizable address. A singleton, like <see cref="FakeSiteThemePreviewRenderer"/>, so the test
/// reads what the app service asked.
/// </summary>
[ExposeServices(typeof(ISiteThemePreviewLinkIssuer), typeof(FakeSiteThemePreviewLinkIssuer))]
public class FakeSiteThemePreviewLinkIssuer : ISiteThemePreviewLinkIssuer, ISingletonDependency
{
    public List<SiteThemePreviewLinkRequest> Requests { get; } = new();

    public Task<SiteThemePreviewLinkDto> IssueAsync(SiteThemePreviewLinkRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        var path = string.IsNullOrEmpty(request.Path) ? "/" : request.Path;
        return Task.FromResult(new SiteThemePreviewLinkDto
        {
            Url = "https://site.example" + path + "?site-preview=token",
            Path = path,
            ReleaseId = request.ReleaseId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
    }
}
