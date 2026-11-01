using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Site.Themes;

/// <summary>A file to analyze: its path and its body.</summary>
public sealed record SiteThemeSourceFile(string Path, string Body);

/// <summary>A page, as far as the theme is concerned: its name and the (normalized) template it asks for.</summary>
public sealed record SiteThemePageTemplate(string PageName, string Template);

/// <summary>One finding of a theme check - see <see cref="SiteThemeIssueCodes"/>.</summary>
public sealed record SiteThemeIssue(
    SiteThemeIssueSeverity Severity,
    string Code,
    string? Path,
    int? Line,
    int? Column,
    IReadOnlyDictionary<string, string> Data);

/// <summary>How a file is used. See <see cref="SiteThemeAnalysis.GetUsages"/>.</summary>
public enum SiteThemeUsageKind
{
    /// <summary>A page's <c>Template</c> names it.</summary>
    Page = 0,

    /// <summary>A template <c>include</c>s it.</summary>
    Include = 1,

    /// <summary>A template writes its address with <c>asset_url</c>.</summary>
    AssetUrl = 2,

    /// <summary>A page template's front matter chooses it as its layout.</summary>
    Layout = 3,

    /// <summary>The theme's default layout setting names it.</summary>
    DefaultLayout = 4
}

/// <summary>One use of a file: by a page (<see cref="PageName"/>), by another file (<see cref="SourcePath"/>), or by the setting.</summary>
public sealed record SiteThemeUsage(SiteThemeUsageKind Kind, string? SourcePath, string? PageName);

/// <summary>What the analysis learned about one file.</summary>
public sealed class SiteThemeFileAnalysis
{
    internal SiteThemeFileAnalysis(string path, string body)
    {
        Path = path;
        Body = body;
        IsAsset = SiteThemePaths.IsAsset(path);
    }

    public string Path { get; }

    public string Body { get; }

    public bool IsAsset { get; }

    /// <summary>The parsed template; null for a static file.</summary>
    public Scriban.Template? Template { get; internal set; }

    public IReadOnlyList<ScribanTemplateError> SyntaxErrors { get; internal set; } = Array.Empty<ScribanTemplateError>();

    public SiteTemplateFrontMatter FrontMatter { get; internal set; } = SiteTemplateFrontMatter.Empty;

    /// <summary>What the body names with <c>include</c> and <c>asset_url</c> - the front matter's layout is in <see cref="FrontMatter"/>.</summary>
    public IReadOnlyList<SiteThemeReference> References { get; internal set; } = Array.Empty<SiteThemeReference>();

    public bool CallsSiteHead { get; internal set; }

    public bool UsesBody { get; internal set; }
}

/// <summary>The result of <see cref="SiteThemeAnalyzer.Analyze"/>.</summary>
public sealed class SiteThemeAnalysis
{
    private readonly Dictionary<string, List<SiteThemeUsage>> _usages;

    internal SiteThemeAnalysis(
        IReadOnlyDictionary<string, SiteThemeFileAnalysis> files,
        IReadOnlyList<SiteThemeIssue> issues,
        Dictionary<string, List<SiteThemeUsage>> usages)
    {
        Files = files;
        Issues = issues;
        _usages = usages;
    }

    /// <summary>Every file, by path.</summary>
    public IReadOnlyDictionary<string, SiteThemeFileAnalysis> Files { get; }

    /// <summary>Errors first, then warnings; within each, by path.</summary>
    public IReadOnlyList<SiteThemeIssue> Issues { get; }

    public bool HasErrors => Issues.Any(issue => issue.Severity == SiteThemeIssueSeverity.Error);

    /// <summary>Who uses <paramref name="path"/>: pages, other files' literal references, the default layout setting.</summary>
    public IReadOnlyList<SiteThemeUsage> GetUsages(string path)
    {
        return _usages.TryGetValue(path, out var usages) ? usages : Array.Empty<SiteThemeUsage>();
    }
}

/// <summary>
/// Reads a theme - its files, its default layout setting and the templates the site's pages ask for - and works out
/// what each file is used as and what is wrong. Pure: no repository, no clock, so the check before a publish, the
/// check an author runs by hand, the delete confirmation and a rename all see the same answer.
/// <para>
/// Only literal names count - <c>include "blog/_sidebar"</c>, <c>asset_url "assets/site.css"</c>,
/// <c>layout = "layouts/landing"</c> - since a name computed at run time cannot be known without rendering. A file
/// has no type of its own beyond template or static file (<see cref="SiteThemePaths"/>); being a page template, a
/// partial or a layout follows from what refers to it, and one file may be several.
/// </para>
/// </summary>
public static class SiteThemeAnalyzer
{
    public static SiteThemeAnalysis Analyze(
        IEnumerable<SiteThemeSourceFile> files,
        string? defaultLayout,
        IEnumerable<SiteThemePageTemplate> pages)
    {
        var analyses = new Dictionary<string, SiteThemeFileAnalysis>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            analyses[file.Path] = Parse(file);
        }

        var context = new AnalysisContext(analyses, defaultLayout);
        context.Run(pages.ToList());

        return new SiteThemeAnalysis(analyses, context.GetSortedIssues(), context.Usages);
    }

    private static SiteThemeFileAnalysis Parse(SiteThemeSourceFile file)
    {
        var analysis = new SiteThemeFileAnalysis(file.Path, file.Body);
        if (analysis.IsAsset)
        {
            return analysis;
        }

        var template = ScribanTemplateSyntax.Parse(file.Body, file.Path);
        analysis.Template = template;
        analysis.SyntaxErrors = ScribanTemplateSyntax.GetErrors(template);

        if (analysis.SyntaxErrors.Count == 0)
        {
            analysis.FrontMatter = ScribanTemplateSyntax.ReadFrontMatter(template);
            analysis.References = ScribanTemplateSyntax.GetReferences(template);
            analysis.CallsSiteHead = ScribanTemplateSyntax.CallsSiteHead(template);
            analysis.UsesBody = ScribanTemplateSyntax.UsesBody(template);
        }

        return analysis;
    }

    private sealed class AnalysisContext
    {
        private readonly Dictionary<string, SiteThemeFileAnalysis> _files;
        private readonly string? _defaultLayout;
        private readonly List<SiteThemeIssue> _issues = new();

        private readonly HashSet<string> _pageTemplates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _included = new(StringComparer.Ordinal);
        private readonly HashSet<string> _layouts = new(StringComparer.Ordinal);

        public AnalysisContext(Dictionary<string, SiteThemeFileAnalysis> files, string? defaultLayout)
        {
            _files = files;
            _defaultLayout = string.IsNullOrWhiteSpace(defaultLayout) ? null : SiteThemePaths.Normalize(defaultLayout);
        }

        public Dictionary<string, List<SiteThemeUsage>> Usages { get; } = new(StringComparer.Ordinal);

        public void Run(List<SiteThemePageTemplate> pages)
        {
            ReportSyntaxAndFrontMatter();
            ReadPages(pages);
            ReadDefaultLayout();
            ReadReferences();
            ReportIncludeCycles();
            ReportLayouts();
            ReportPagesWithoutLayout();
            ReportUnused();
        }

        public List<SiteThemeIssue> GetSortedIssues()
        {
            return _issues
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.Path ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(issue => issue.Line ?? 0)
                .ThenBy(issue => issue.Column ?? 0)
                .ToList();
        }

        private void ReportSyntaxAndFrontMatter()
        {
            foreach (var file in _files.Values.Where(file => !file.IsAsset))
            {
                foreach (var error in file.SyntaxErrors)
                {
                    Error(SiteThemeIssueCodes.SyntaxError, file.Path, error.Line, error.Column, ("Message", error.Message));
                }

                foreach (var error in file.FrontMatter.Errors)
                {
                    Error(SiteThemeIssueCodes.FrontMatterInvalid, file.Path, error.Line, error.Column, ("Message", error.Message));
                }
            }
        }

        private void ReadPages(List<SiteThemePageTemplate> pages)
        {
            foreach (var group in pages
                         .Where(page => !string.IsNullOrWhiteSpace(page.Template))
                         .GroupBy(page => SiteThemePaths.Normalize(page.Template), StringComparer.Ordinal))
            {
                var template = group.Key;
                var pageNames = group.Select(page => page.PageName).OrderBy(name => name, StringComparer.Ordinal).ToList();

                if (_files.TryGetValue(template, out var file) && !file.IsAsset)
                {
                    _pageTemplates.Add(template);
                    foreach (var pageName in pageNames)
                    {
                        AddUsage(template, new SiteThemeUsage(SiteThemeUsageKind.Page, null, pageName));
                    }

                    continue;
                }

                Warning(SiteThemeIssueCodes.PageRenderedByRazor, null, null, null,
                    ("Template", template), ("Pages", string.Join(", ", pageNames)));
            }
        }

        private void ReadDefaultLayout()
        {
            if (_defaultLayout == null)
            {
                return;
            }

            if (_files.TryGetValue(_defaultLayout, out var file) && !file.IsAsset)
            {
                AddUsage(_defaultLayout, new SiteThemeUsage(SiteThemeUsageKind.DefaultLayout, null, null));
                _layouts.Add(_defaultLayout);
                return;
            }

            Error(SiteThemeIssueCodes.DefaultLayoutMissing, null, null, null, ("Layout", _defaultLayout));
        }

        private void ReadReferences()
        {
            foreach (var file in _files.Values.Where(file => !file.IsAsset))
            {
                foreach (var reference in file.References)
                {
                    ReadReference(file, reference);
                }

                if (file.FrontMatter.Layout is { } layout && layout.Path != SiteThemeConsts.NoLayout)
                {
                    ReadReference(file, layout);
                }
            }

            // The layout a page template is rendered in: what its front matter says, else the default. Only a page
            // template's front matter matters - an included file's or a layout's is ignored when rendering.
            foreach (var pageTemplate in _pageTemplates)
            {
                if (GetEffectiveLayout(pageTemplate) is { } layout && IsTemplate(layout))
                {
                    _layouts.Add(layout);
                }
            }
        }

        private void ReadReference(SiteThemeFileAnalysis source, SiteThemeReference reference)
        {
            var target = reference.Path;
            var function = reference.FunctionName;

            if (!_files.TryGetValue(target, out var targetFile))
            {
                Error(SiteThemeIssueCodes.ReferenceMissing, source.Path, reference.Line, reference.Column,
                    ("Function", function), ("Target", target));
                return;
            }

            switch (function)
            {
                case SiteThemeConsts.AssetUrlFunctionName when !targetFile.IsAsset:
                    Error(SiteThemeIssueCodes.AssetUrlToTemplate, source.Path, reference.Line, reference.Column, ("Target", target));
                    return;
                case SiteThemeConsts.IncludeFunctionName or SiteThemeConsts.LayoutKey when targetFile.IsAsset:
                    Error(SiteThemeIssueCodes.ReferenceToStaticFile, source.Path, reference.Line, reference.Column,
                        ("Function", function), ("Target", target));
                    return;
            }

            var kind = function switch
            {
                SiteThemeConsts.IncludeFunctionName => SiteThemeUsageKind.Include,
                SiteThemeConsts.AssetUrlFunctionName => SiteThemeUsageKind.AssetUrl,
                _ => SiteThemeUsageKind.Layout
            };

            if (kind == SiteThemeUsageKind.Include)
            {
                _included.Add(target);
            }

            AddUsage(target, new SiteThemeUsage(kind, source.Path, null));
        }

        private void ReportIncludeCycles()
        {
            var reported = new HashSet<string>(StringComparer.Ordinal);
            var done = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in _files.Values.Where(file => !file.IsAsset))
            {
                FindCycles(file.Path, new List<string>(), done, reported);
            }
        }

        private void FindCycles(string path, List<string> chain, HashSet<string> done, HashSet<string> reported)
        {
            var index = chain.IndexOf(path);
            if (index >= 0)
            {
                var cycle = chain.Skip(index).Append(path).ToList();
                // One report per cycle, however many of its files the walk starts from.
                var key = string.Join("|", cycle.Skip(1).OrderBy(p => p, StringComparer.Ordinal));
                if (reported.Add(key))
                {
                    Error(SiteThemeIssueCodes.IncludeCycle, cycle[0], null, null, ("Chain", string.Join(" -> ", cycle)));
                }

                return;
            }

            if (done.Contains(path))
            {
                return;
            }

            chain.Add(path);
            foreach (var target in GetIncludedTemplates(path))
            {
                FindCycles(target, chain, done, reported);
            }

            chain.RemoveAt(chain.Count - 1);
            done.Add(path);
        }

        private void ReportLayouts()
        {
            foreach (var layout in _layouts.OrderBy(path => path, StringComparer.Ordinal))
            {
                var file = _files[layout];
                if (file.SyntaxErrors.Count > 0)
                {
                    continue;
                }

                if (file.FrontMatter.Exists)
                {
                    Error(SiteThemeIssueCodes.FrontMatterInLayout, layout, null, null);
                }

                if (!Reaches(layout, f => f.CallsSiteHead))
                {
                    Error(SiteThemeIssueCodes.LayoutWithoutSiteHead, layout, null, null);
                }

                if (!Reaches(layout, f => f.UsesBody))
                {
                    Warning(SiteThemeIssueCodes.LayoutWithoutBody, layout, null, null);
                }

                var roles = new List<string>();
                if (_pageTemplates.Contains(layout))
                {
                    roles.Add("page");
                }

                if (_included.Contains(layout))
                {
                    roles.Add(SiteThemeConsts.IncludeFunctionName);
                }

                if (roles.Count > 0)
                {
                    Warning(SiteThemeIssueCodes.MixedRoles, layout, null, null, ("Roles", string.Join(", ", roles)));
                }
            }
        }

        private void ReportPagesWithoutLayout()
        {
            foreach (var pageTemplate in _pageTemplates.OrderBy(path => path, StringComparer.Ordinal))
            {
                var file = _files[pageTemplate];
                if (file.SyntaxErrors.Count > 0 || GetEffectiveLayout(pageTemplate) != null)
                {
                    continue;
                }

                if (!Reaches(pageTemplate, f => f.CallsSiteHead))
                {
                    Error(SiteThemeIssueCodes.PageWithoutSiteHead, pageTemplate, null, null);
                }
            }
        }

        private void ReportUnused()
        {
            foreach (var path in _files.Keys.OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!Usages.ContainsKey(path))
                {
                    Warning(SiteThemeIssueCodes.UnusedFile, path, null, null);
                }
            }
        }

        /// <summary>
        /// The layout <paramref name="pageTemplate"/> renders in, or null for none: its front matter's
        /// <c>layout</c>, else the default layout setting. May name a file that does not exist - reported apart.
        /// </summary>
        private string? GetEffectiveLayout(string pageTemplate)
        {
            var layout = _files[pageTemplate].FrontMatter.Layout?.Path ?? _defaultLayout;
            return layout == SiteThemeConsts.NoLayout ? null : layout;
        }

        /// <summary>Whether <paramref name="path"/>, or a template it includes, directly or not, satisfies <paramref name="predicate"/>.</summary>
        private bool Reaches(string path, Func<SiteThemeFileAnalysis, bool> predicate)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(path);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (predicate(_files[current]))
                {
                    return true;
                }

                foreach (var target in GetIncludedTemplates(current))
                {
                    pending.Push(target);
                }
            }

            return false;
        }

        /// <summary>The existing templates <paramref name="path"/> includes by a literal name.</summary>
        private IEnumerable<string> GetIncludedTemplates(string path)
        {
            return _files[path].References
                .Where(reference => reference.FunctionName == SiteThemeConsts.IncludeFunctionName && IsTemplate(reference.Path))
                .Select(reference => reference.Path)
                .Distinct(StringComparer.Ordinal);
        }

        private bool IsTemplate(string path)
        {
            return _files.TryGetValue(path, out var file) && !file.IsAsset;
        }

        private void AddUsage(string path, SiteThemeUsage usage)
        {
            if (!Usages.TryGetValue(path, out var usages))
            {
                Usages[path] = usages = new List<SiteThemeUsage>();
            }

            // A file that includes the same partial three times is one use of it.
            if (!usages.Contains(usage))
            {
                usages.Add(usage);
            }
        }

        private void Error(string code, string? path, int? line, int? column, params (string Key, string Value)[] data)
        {
            Add(SiteThemeIssueSeverity.Error, code, path, line, column, data);
        }

        private void Warning(string code, string? path, int? line, int? column, params (string Key, string Value)[] data)
        {
            Add(SiteThemeIssueSeverity.Warning, code, path, line, column, data);
        }

        private void Add(SiteThemeIssueSeverity severity, string code, string? path, int? line, int? column, (string Key, string Value)[] data)
        {
            _issues.Add(new SiteThemeIssue(
                severity,
                code,
                path,
                line,
                column,
                data.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));
        }
    }
}
