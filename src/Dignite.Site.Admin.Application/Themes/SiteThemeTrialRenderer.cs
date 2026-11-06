using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
/// own address in every language it is served in, one content for each content type and language under it, and the
/// second page of a list that has more than one content - a list, a detail and a later page run different branches of
/// the same template. Published contents only - what visitors see - and a bounded number of renders in a bounded time
/// (<see cref="MaxPaths"/>, <see cref="TimeBudget"/>). Within the bound, each template version (<c>Blog/Post</c>,
/// <c>Blog/Post.ja</c>) is rendered once as a list, once per content type as a detail and once as a later page before any
/// is rendered a second time, so a large site still has every branch run; what the bound leaves out is reported as a
/// warning, not skipped silently, and the warning says which bound it was.
/// </para>
/// <para>
/// Only the theme's failures are findings: a template that fails at run time or never calls <c>site_head</c>. A path that
/// resolves to nothing, a page the theme does not render, or any failure of a service along the way is not the theme's, and
/// is left out. The renderer is the preview's (<see cref="SiteThemePreviewRendererProvider"/>): an application with none
/// renders nothing, and says so in a warning when the theme renders any page - a check that rendered nothing must not
/// read as a check whose pages rendered well.
/// </para>
/// </summary>
public class SiteThemeTrialRenderer : ITransientDependency
{
    /// <summary>The most addresses rendered in one run.</summary>
    public const int MaxPaths = 100;

    /// <summary>
    /// The query string of a list's second page: the site's default page parameter (<c>SiteScribanOptions.PageQueryKey</c>).
    /// A site that names it otherwise renders its first page again, which finds nothing new but breaks nothing.
    /// </summary>
    public const string NextPageQueryString = "?page=2";

    /// <summary>The most contents read per page to pick the sample from; they are ordered by language and slug.</summary>
    public const int MaxContentsPerPage = 200;

    /// <summary>The most dangling-reference warnings one run reports; the rest would only repeat the same mistake.</summary>
    public const int MaxFragmentWarnings = 20;

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
    /// the sample was cut short or could not be rendered at all. Empty when nothing failed, or when the theme renders no
    /// page.
    /// </summary>
    public virtual async Task<List<SiteThemeIssue>> RenderAsync(
        SiteThemeSnapshotDto snapshot,
        SiteThemeAnalysis analysis,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<SiteThemeIssue>();

        var paths = await GetSamplePathsAsync(analysis, cancellationToken);
        if (paths.Count == 0)
        {
            return issues;
        }

        var renderer = FindRenderer();
        if (renderer == null)
        {
            issues.Add(new SiteThemeIssue(
                SiteThemeIssueSeverity.Warning,
                SiteThemeIssueCodes.TrialRenderUnavailable,
                null,
                null,
                null,
                new Dictionary<string, string>
                {
                    ["Total"] = paths.Count.ToString(CultureInfo.InvariantCulture)
                }));

            return issues;
        }

        var failures = new Dictionary<string, SiteThemeIssue>(StringComparer.Ordinal);
        var fragmentWarnings = new Dictionary<string, SiteThemeIssue>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        var rendered = 0;
        var outOfTime = false;
        string? stoppedAt = null;

        foreach (var path in paths.Take(MaxPaths))
        {
            if (stopwatch.Elapsed > TimeBudget)
            {
                outOfTime = true;
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var preview = await renderer.RenderAsync(new SiteThemePreviewRequest { Path = path, Snapshot = snapshot }, cancellationToken);

                foreach (var id in RenderedFragmentReferences.FindMissing(preview.Html))
                {
                    if (fragmentWarnings.Count < MaxFragmentWarnings)
                    {
                        fragmentWarnings.TryAdd(id, new SiteThemeIssue(
                            SiteThemeIssueSeverity.Warning,
                            SiteThemeIssueCodes.FragmentReferenceNotFound,
                            null,
                            null,
                            null,
                            new Dictionary<string, string> { ["Path"] = path, ["Id"] = id }));
                    }
                }
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
                stoppedAt = path;
                break;
            }

            rendered++;
        }

        issues.AddRange(failures.Values);
        issues.AddRange(fragmentWarnings.Values);

        if (rendered < paths.Count)
        {
            issues.Add(ToIncompleteIssue(rendered, paths.Count, outOfTime, stoppedAt));
        }

        return issues;
    }

    /// <summary>
    /// The renderer of <c>Dignite.Site.Public.Web</c>, or the site's over HTTP for an administration API deployed apart
    /// from it, or null with neither - see <see cref="SiteThemePreviewRendererProvider"/>.
    /// </summary>
    protected virtual ISiteThemePreviewRenderer? FindRenderer()
    {
        return LazyServiceProvider.LazyGetRequiredService<SiteThemePreviewRendererProvider>().Find();
    }

    /// <summary>
    /// Why the run did not render the whole sample, as a warning: a service failed (<see cref="SiteThemeIssueCodes.TrialRenderStopped"/>),
    /// the time ran out (<see cref="SiteThemeIssueCodes.TrialRenderOutOfTime"/>), or the sample is larger than a run
    /// renders (<see cref="SiteThemeIssueCodes.TrialRenderIncomplete"/>).
    /// </summary>
    protected virtual SiteThemeIssue ToIncompleteIssue(int rendered, int total, bool outOfTime, string? stoppedAt)
    {
        var data = new Dictionary<string, string>
        {
            ["Rendered"] = rendered.ToString(CultureInfo.InvariantCulture),
            ["Total"] = total.ToString(CultureInfo.InvariantCulture)
        };

        string code;
        if (stoppedAt != null)
        {
            code = SiteThemeIssueCodes.TrialRenderStopped;
            data["Path"] = stoppedAt;
        }
        else if (outOfTime)
        {
            code = SiteThemeIssueCodes.TrialRenderOutOfTime;
            data["Seconds"] = ((int)TimeBudget.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            code = SiteThemeIssueCodes.TrialRenderIncomplete;
            data["Max"] = MaxPaths.ToString(CultureInfo.InvariantCulture);
        }

        return new SiteThemeIssue(SiteThemeIssueSeverity.Warning, code, null, null, null, data);
    }

    /// <summary>
    /// The addresses to render: for each routable page that renders through a template of the theme, its own address in
    /// the default language and in every other served language that has content under it, one published content per
    /// content type and language, and the second page of the list in each language with more than one content.
    /// <para>
    /// Ordered so that a run cut short still runs every branch it can: first one address for each template version the
    /// pages render through and each way it renders - as a list, as a detail per content type, as a later page - in the
    /// order of the pages' routes; then the rest, in the same order.
    /// </para>
    /// </summary>
    protected virtual async Task<List<string>> GetSamplePathsAsync(SiteThemeAnalysis analysis, CancellationToken cancellationToken)
    {
        var languages = await LanguageProvider.GetEnabledLanguagesAsync(cancellationToken);

        var context = new SiteLanguages(languages[0], languages);
        var asOf = Clock.Now;

        var first = new List<string>();
        var rest = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        var seenKinds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var page in (await PageRepository.GetRoutableListAsync(cancellationToken)).OrderBy(p => p.Route, StringComparer.Ordinal))
        {
            if (!IsRenderedByTheme(page, analysis))
            {
                continue;
            }

            var template = GetTemplatePath(page);

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
                Add(context.ApplyCulturePrefix(page.GetPath(), culture), GetTemplateVersion(template, culture, analysis) + "|list");
            }

            foreach (var group in contents.GroupBy(content => (content.ContentTypeId, content.CultureName)))
            {
                var content = group.First();
                Add(
                    context.ApplyCulturePrefix(page.BuildContentPath(content), content.CultureName),
                    GetTemplateVersion(template, content.CultureName, analysis) + "|detail|" + content.ContentTypeId.ToString("N"));
            }

            foreach (var culture in contents
                         .GroupBy(content => content.CultureName)
                         .Where(group => group.Skip(1).Any())
                         .Select(group => group.Key))
            {
                Add(
                    context.ApplyCulturePrefix(page.GetPath(), culture) + NextPageQueryString,
                    GetTemplateVersion(template, culture, analysis) + "|next");
            }
        }

        first.AddRange(rest);
        return first;

        void Add(string path, string kind)
        {
            if (seenPaths.Add(path))
            {
                (seenKinds.Add(kind) ? first : rest).Add(path);
            }
        }
    }

    /// <summary>The theme path of the page's template, the same reading the renderer makes of <c>Template</c>.</summary>
    protected virtual string GetTemplatePath(Page page)
    {
        var template = page.Template!.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
            ? page.Template[..^".cshtml".Length]
            : page.Template;

        return SiteThemePaths.Normalize(template);
    }

    /// <summary>
    /// The file a page in <paramref name="cultureName"/> renders through: the template's version for that language or
    /// the nearest parent language that has one (<c>Default.zh-Hant</c>, <c>Default.zh</c>), else the template itself -
    /// the renderer's choice, to tell the template versions of a sample apart.
    /// </summary>
    protected virtual string GetTemplateVersion(string template, string cultureName, SiteThemeAnalysis analysis)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return template;
        }

        for (; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            var version = SiteThemePaths.GetLanguageVersionPath(template, culture.Name);
            if (analysis.Files.ContainsKey(version))
            {
                return version;
            }
        }

        return template;
    }

    /// <summary>Whether the page's template is a template of the theme - the same reading the renderer makes of <c>Template</c>.</summary>
    protected virtual bool IsRenderedByTheme(Page page, SiteThemeAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(page.Template))
        {
            return false;
        }

        return analysis.Files.TryGetValue(GetTemplatePath(page), out var file) && !file.IsAsset;
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
