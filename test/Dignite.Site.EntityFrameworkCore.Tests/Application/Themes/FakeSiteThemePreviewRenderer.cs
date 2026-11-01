using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// Stands in for <c>Dignite.Site.Public.Web</c>'s renderer, which this test host does not load: records each request
/// and answers with the snapshot's <c>Default</c> template, wrapped so a test can see which snapshot arrived.
/// <see cref="Failure"/>, when set, is thrown instead - a template that fails to render.
/// <para>
/// Registered by convention for every test in this assembly; resolve it as itself to read what it recorded. A
/// singleton, so the instance the app service resolves is the one the test reads - one per test, since every test
/// builds its own application.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteThemePreviewRenderer), typeof(FakeSiteThemePreviewRenderer))]
public class FakeSiteThemePreviewRenderer : ISiteThemePreviewRenderer, ISingletonDependency
{
    public List<SiteThemePreviewRequest> Requests { get; } = new();

    public Exception? Failure { get; set; }

    public Task<SiteThemePreviewDto> RenderAsync(SiteThemePreviewRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        if (Failure != null)
        {
            throw Failure;
        }

        var page = request.Snapshot.Files.FirstOrDefault(file => file.Path == "Default")?.Body ?? string.Empty;
        var release = request.Snapshot.ReleaseNumber?.ToString() ?? "working-copy";

        return Task.FromResult(new SiteThemePreviewDto
        {
            Html = $"<html data-release=\"{release}\">{page}</html>",
            Path = request.Path,
            PageName = "about",
            CultureName = "en",
            PageTemplate = "Default",
            Layout = request.Snapshot.DefaultLayout
        });
    }
}
