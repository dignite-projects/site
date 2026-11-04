using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
using Dignite.Site.Themes;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// Renders a sample of the site's pages with the working copy, to find what only a render finds: the check reads the
/// templates but runs none, so a variable that does not exist, a filter that does not take what it is given or a
/// partial that fails on one kind of content passes it and breaks the page the first visitor opens.
/// <para>
/// The sample is the addresses a visitor would open, per page that renders through a template of the theme: the page's
/// own address in every language it is served in, and one content for each content type and language under it, since a
/// list and a detail run different branches of the same template. Published contents only - what visitors see - and a
/// bounded number of renders in a bounded time (<see cref="MaxPaths"/>, <see cref="TimeBudget"/>); what the bound leaves
/// out is reported as a warning, not skipped silently.
/// </para>
/// <para>
/// Only the theme's failures are findings: a template that fails at run time or never calls <c>site_head</c>. A path that
/// resolves to nothing, a page the theme does not render, or any failure of a service along the way is not the theme's, and
/// is left out. The renderer is <c>Dignite.Site.Public.Web</c>'s, resolved optionally like the preview's: an application
/// hosted without it renders nothing and finds nothing.
/// </para>
/// </summary>
public class SiteThemeTrialRenderer : ITransientDependency
{
    /// <summary>The most addresses rendered in one run.</summary>
    public const int MaxPaths = 60;

    /// <summary>The most contents read per page to pick the sample from; they are ordered by language and slug.</summary>
    public const int MaxContentsPerPage = 200;

    /// <summary>The longest a run goes on starting renders. A render in flight is not interrupted; it has its own limit.</summary>
    public static readonly TimeSpan TimeBudget = TimeSpan.FromSeconds(30);

    protected IAbpLazyServiceProvider LazyServiceProvider { get; }
    protected IPageRepository PageRepository { get; }
    protected IContentRepository ContentRepository { get; }
    protected SiteLanguageProvider LanguageProvider { get; }
    protected IClock Clock { get; }
    protected ILogger<SiteThemeTrialRenderer> Logger { get; }

    public SiteThemeTrialRenderer(
        IAbpLazyServiceProvider lazyServiceProvider,
        IPageRepository pageRepository,
        IContentRepository contentRepository,
        SiteLanguageProvider languageProvider,
        IClock clock,
        ILogger<SiteThemeTrialRenderer> logger)
    {
        LazyServiceProvider = lazyServiceProvider;
        PageRepository = pageRepository;
        ContentRepository = contentRepository;
        LanguageProvider = languageProvider;
        Clock = clock;
        Logger = logger;
    }

    /// <summary>
    /// What rendering the sample with <paramref name="snapshot"/> found: an error per distinct failure, and a warning when
    /// the sample was cut short. Empty when nothing failed, or when there is no renderer to render with.
    /// </summary>
    public virtual async Task<List<SiteThemeIssue>> RenderAsync(
        SiteThemeSnapshotDto snapshot,
        SiteThemeAnalysis analysis,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<SiteThemeIssue>();

        var renderer = LazyServiceProvider.LazyGetService<ISiteThemePreviewRenderer>();
        if (renderer == null)
        {
            return issues;
        }

        var paths = await GetSamplePathsAsync(analysis, cancellationToken);
        var failures = new Dictionary<string, SiteThemeIssue>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        var rendered = 0;

        foreach (var path in paths.Take(MaxPaths))
        {
            if (stopwatch.Elapsed > TimeBudget)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await renderer.RenderAsync(new SiteThemePreviewRequest { Path = path, Snapshot = snapshot }, cancellationToken);
            }
            catch (BusinessException exception) when (exception.Code is
                                                          SiteErrorCodes.SiteTemplatePreviewRenderFailed
                                                          or SiteErrorCodes.SiteTemplateDidNotRenderSiteHead)
            {
                var message = exception.Data["Message"] as string ?? exception.Code!;
                failures.TryAdd(message, ToIssue(analysis, path, message, exception));
            }
            catch (BusinessException)
            {
                // No page answers the path, or a Razor view renders the page: nothing the theme did.
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A service met a problem rendering: the application's, not the theme's. It would meet it for the next
                // path too, so the run ends here instead of repeating it for every address - and says so below, since
                // the addresses it did not render are not addresses that rendered well.
                Logger.LogWarning(exception, "The trial render of the theme stopped at '{Path}'.", path);
                break;
            }

            rendered++;
        }

        issues.AddRange(failures.Values);

        if (rendered < paths.Count)
        {
            issues.Add(new SiteThemeIssue(
                SiteThemeIssueSeverity.Warning,
                SiteThemeIssueCodes.TrialRenderIncomplete,
                null,
                null,
                null,
                new Dictionary<string, string>
                {
                    ["Rendered"] = rendered.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["Total"] = paths.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        return issues;
    }

    /// <summary>
    /// The addresses to render: for each routable page that renders through a template of the theme, its own address in
    /// the default language and in every other served language that has content under it, then one published content per
    /// content type and language. In the order of the pages' routes, each page's own addresses first.
    /// </summary>
    protected virtual async Task<List<string>> GetSamplePathsAsync(SiteThemeAnalysis analysis, CancellationToken cancellationToken)
    {
        var languages = await LanguageProvider.GetEnabledLanguagesAsync(cancellationToken);

        var context = new SiteLanguages(languages[0], languages);
        var asOf = Clock.Now;

        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var page in (await PageRepository.GetRoutableListAsync(cancellationToken)).OrderBy(p => p.Route, StringComparer.Ordinal))
        {
            if (!IsRenderedByTheme(page, analysis))
            {
                continue;
            }

            var contents = await ContentRepository.GetListAsync(
                pageId: page.Id,
                status: ContentStatus.Published,
                publishedBefore: asOf,
                maxResultCount: MaxContentsPerPage,
                sorting: $"{nameof(Content.CultureName)},{nameof(Content.Slug)}",
                cancellationToken: cancellationToken);

            contents = contents.Where(content => context.IsServed(content.CultureName)).ToList();

            var cultures = new List<string> { context.DefaultCultureName };
            cultures.AddRange(contents
                .Select(content => content.CultureName)
                .Distinct(StringComparer.Ordinal)
                .Where(culture => !string.Equals(culture, context.DefaultCultureName, StringComparison.Ordinal))
                .OrderBy(culture => culture, StringComparer.Ordinal));

            foreach (var culture in cultures)
            {
                Add(context.ApplyCulturePrefix(page.GetPath(), culture));
            }

            foreach (var group in contents.GroupBy(content => (content.ContentTypeId, content.CultureName)))
            {
                var content = group.First();
                Add(context.ApplyCulturePrefix(page.BuildContentPath(content), content.CultureName));
            }
        }

        return paths;

        void Add(string path)
        {
            if (seen.Add(path))
            {
                paths.Add(path);
            }
        }
    }

    /// <summary>Whether the page's template is a template of the theme - the same reading the renderer makes of <c>Template</c>.</summary>
    protected virtual bool IsRenderedByTheme(Page page, SiteThemeAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(page.Template))
        {
            return false;
        }

        var template = page.Template.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
            ? page.Template[..^".cshtml".Length]
            : page.Template;

        return analysis.Files.TryGetValue(SiteThemePaths.Normalize(template), out var file) && !file.IsAsset;
    }

    /// <summary>
    /// The failure as a finding. When the renderer says where in a template of the theme it happened (file, line and column)
    /// that becomes the finding's place; otherwise the finding is about the page's template as a whole.
    /// </summary>
    protected virtual SiteThemeIssue ToIssue(SiteThemeAnalysis analysis, string samplePath, string message, BusinessException failure)
    {
        string? file = null;
        int? line = null;
        int? column = null;

        if (failure.Data["File"] is string located && analysis.Files.ContainsKey(SiteThemePaths.Normalize(located)))
        {
            file = SiteThemePaths.Normalize(located);
            line = failure.Data["Line"] as int?;
            column = failure.Data["Column"] as int?;
        }

        return new SiteThemeIssue(
            SiteThemeIssueSeverity.Error,
            SiteThemeIssueCodes.RenderFailed,
            file,
            line,
            column,
            new Dictionary<string, string> { ["Path"] = samplePath, ["Message"] = message });
    }
}
