using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Stands in for <c>Dignite.Site.Public.Web</c>'s link issuer, which this test host does not load: records each
/// request and answers with a link that spells out what it was asked for, so a test can see the name, version,
/// path and base URL that arrived.
/// <para>
/// Registered by convention for every test in this assembly, like <see cref="FakeSiteTemplatePreviewRenderer"/>;
/// resolve it as itself to read what it recorded. A singleton, one per test, since every test builds its own
/// application.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteTemplatePreviewLinkIssuer), typeof(FakeSiteTemplatePreviewLinkIssuer))]
public class FakeSiteTemplatePreviewLinkIssuer : ISiteTemplatePreviewLinkIssuer, ISingletonDependency
{
    public static readonly DateTime ExpiresAt = new(2026, 7, 15, 9, 40, 0, DateTimeKind.Utc);

    public List<SiteTemplatePreviewLinkRequest> Requests { get; } = new();

    public Task<SiteTemplatePreviewLinkDto> IssueAsync(SiteTemplatePreviewLinkRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        var path = string.IsNullOrEmpty(request.Path) ? "/" : request.Path;

        return Task.FromResult(new SiteTemplatePreviewLinkDto
        {
            Url = $"{request.BaseUrl ?? "https://request.example"}{path}?site-preview=token-{request.VersionNumber}",
            Path = path,
            TemplateName = request.TemplateName,
            VersionNumber = request.VersionNumber,
            ExpiresAt = ExpiresAt
        });
    }
}
