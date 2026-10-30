using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Stands in for <c>Dignite.Site.Public.Web</c>'s renderer, which this test host does not load: records each
/// request and answers with the version it was handed, wrapped so a test can see which template and version
/// arrived. <see cref="Failure"/>, when set, is thrown instead - a template that fails to render.
/// <para>
/// Registered by convention for every test in this assembly; resolve it as itself to read what it recorded.
/// A singleton, so the instance the app service resolves is the one the test reads - one per test, since every
/// test builds its own application.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteTemplatePreviewRenderer), typeof(FakeSiteTemplatePreviewRenderer))]
public class FakeSiteTemplatePreviewRenderer : ISiteTemplatePreviewRenderer, ISingletonDependency
{
    public List<SiteTemplatePreviewRequest> Requests { get; } = new();

    public Exception? Failure { get; set; }

    public Task<SiteTemplatePreviewDto> RenderAsync(SiteTemplatePreviewRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        if (Failure != null)
        {
            throw Failure;
        }

        return Task.FromResult(new SiteTemplatePreviewDto
        {
            Html = $"<html data-template=\"{request.TemplateName}\" data-version=\"{request.Version.Number}\">{request.Version.Body}</html>",
            Path = request.Path,
            PageName = "about",
            CultureName = "en",
            PageTemplate = "Default",
            VersionNumber = request.Version.Number
        });
    }
}
