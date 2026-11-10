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

    /// <summary>What a file of the theme's texts (<c>locales/ja.json</c>) holds; null for any other file.</summary>
    public SiteThemeTextFileReadResult? TextFile { get; internal set; }

    public IReadOnlyList<ScribanTemplateError> SyntaxErrors { get; internal set; } = Array.Empty<ScribanTemplateError>();

    public SiteTemplateFrontMatter FrontMatter { get; internal set; } = SiteTemplateFrontMatter.Empty;

    /// <summary>What the body names with <c>include</c> and <c>asset_url</c> - the front matter's layout is in <see cref="FrontMatter"/>.</summary>
    public IReadOnlyList<SiteThemeReference> References { get; internal set; } = Array.Empty<SiteThemeReference>();

    public bool CallsSiteHead { get; internal set; }

    public bool UsesBody { get; internal set; }

    /// <summary>
    /// The local variables the body reads and never gives a value - what an <c>include</c> of this template has to pass
    /// as named arguments. See <see cref="ScribanTemplateSyntax.GetRequiredLocals"/>.
    /// </summary>
    public IReadOnlyCollection<string> RequiredLocals { get; internal set; } = Array.Empty<string>();
}

/// <summary>The result of <see cref="SiteThemeAnalyzer.Analyze"/>.</summary>
public sealed class SiteThemeAnalysis
{
    private readonly Dictionary<string, List<SiteThemeUsage>> _usages;
    private readonly string? _defaultLayout;

    internal SiteThemeAnalysis(
        IReadOnlyDictionary<string, SiteThemeFileAnalysis> files,
        IReadOnlyList<SiteThemeIssue> issues,
        Dictionary<string, List<SiteThemeUsage>> usages,
        IReadOnlyList<string> layouts,
        string? defaultLayout)
    {
        Files = files;
        Issues = issues;
        _usages = usages;
        Layouts = layouts;
        _defaultLayout = string.IsNullOrWhiteSpace(defaultLayout) ? null : SiteThemePaths.Normalize(defaultLayout);
    }

    /// <summary>
    /// The templates that can serve as a layout, by path: they parse, have no front matter, and write the page's
    /// <c>body</c> and call <c>site_head</c> - themselves or through what they include. A file has no type, so this is
    /// read off what it does, whether or not anything uses it as a layout yet. It is what a default layout must be.
    /// </summary>
    public IReadOnlyList<string> Layouts { get; }

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

    /// <summary>
    /// The files a page rendered through <paramref name="pageTemplate"/> can run: the template itself, the layout it
    /// renders in - its front matter's, else the default layout - and every template either of them includes, directly
    /// or not. A page template may be a language version (<c>Blog/Post.ja</c>); a layout or an include stands for itself
    /// and every language version of it, since the renderer picks the one that fits the page's language.
    /// <para>
    /// Errs on the side of more: a language version of a page template is held to its own front matter's layout and to
    /// its template's. Only literal names count, as everywhere in the analysis - a template that includes a name it
    /// computes at run time runs more than this says.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string> GetRenderedFiles(string pageTemplate)
    {
        var rendered = new HashSet<string>(StringComparer.Ordinal) { pageTemplate };
        var pending = new Stack<string>();
        pending.Push(pageTemplate);

        var owners = new List<string> { pageTemplate };
        if (SiteThemePaths.TryGetLanguageVersion(pageTemplate, out var template, out _))
        {
            owners.Add(template);
        }

        foreach (var owner in owners)
        {
            var layout = Files.TryGetValue(owner, out var file) && file.FrontMatter.Layout is { } chosen
                ? chosen.Path
                : _defaultLayout;
            if (layout != null && layout != SiteThemeConsts.NoLayout)
            {
                AddWithLanguageVersions(layout);
            }
        }

        while (pending.Count > 0)
        {
            if (!Files.TryGetValue(pending.Pop(), out var current) || current.IsAsset)
            {
                continue;
            }

            foreach (var reference in current.References.Where(r => r.FunctionName == SiteThemeConsts.IncludeFunctionName))
            {
                AddWithLanguageVersions(reference.Path);
            }
        }

        return rendered;

        void AddWithLanguageVersions(string path)
        {
            foreach (var candidate in Files.Keys
                         .Where(key => SiteThemePaths.TryGetLanguageVersion(key, out var of, out _) && of == path)
                         .Prepend(path))
            {
                if (rendered.Add(candidate))
                {
                    pending.Push(candidate);
                }
            }
        }
    }
}

/// <summary>
/// Reads a theme - its files, its default layout setting and the templates the site's pages ask for - and works out
/// what each file is used as and what is wrong. Pure: no repository, no clock, so the check before a publish, the
/// check an author runs by hand, the delete confirmation and a rename all see the same answer.
/// <para>
/// Only literal names count - <c>include "blog/_sidebar"</c>, <c>asset_url "css/site.css"</c>,
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

        return new SiteThemeAnalysis(analyses, context.GetSortedIssues(), context.Usages, context.Layouts, defaultLayout);
    }

    private static SiteThemeFileAnalysis Parse(SiteThemeSourceFile file)
    {
        var analysis = new SiteThemeFileAnalysis(file.Path, file.Body);
        if (analysis.IsAsset)
        {
            if (SiteThemeTextFiles.IsTextFile(file.Path))
            {
                analysis.TextFile = SiteThemeTextFiles.Read(file.Path, file.Body);
            }

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
            analysis.RequiredLocals = ScribanTemplateSyntax.GetRequiredLocals(template);
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

        // A template's path to the paths of its language versions (Default to Default.ja, Default.zh-Hant).
        private readonly Dictionary<string, List<string>> _languageVersions = new(StringComparer.Ordinal);

        public AnalysisContext(Dictionary<string, SiteThemeFileAnalysis> files, string? defaultLayout)
        {
            _files = files;
            _defaultLayout = string.IsNullOrWhiteSpace(defaultLayout) ? null : SiteThemePaths.Normalize(defaultLayout);

            foreach (var path in files.Keys.OrderBy(path => path, StringComparer.Ordinal))
            {
                if (SiteThemePaths.TryGetLanguageVersion(path, out var templatePath, out _))
                {
                    if (!_languageVersions.TryGetValue(templatePath, out var versions))
                    {
                        _languageVersions[templatePath] = versions = new List<string>();
                    }

                    versions.Add(path);
                }
            }
        }

        public Dictionary<string, List<SiteThemeUsage>> Usages { get; } = new(StringComparer.Ordinal);

        public List<string> Layouts { get; } = new();

        public void Run(List<SiteThemePageTemplate> pages)
        {
            ReportSyntaxAndFrontMatter();
            ReportTextFiles();
            ReportLanguageVersionsWithoutTemplate();
            ReportPathsThatAreFolders();
            ReadPages(pages);
            ReadDefaultLayout();
            ReadReferences();
            ReadLanguageVersions();
            ReportIncludeCycles();
            ReportLayouts();
            ReportPagesWithoutLayout();
            ReportUnused();
            FindLayouts();
        }

        /// <summary>The templates that could be a layout, whatever uses them now; see <see cref="SiteThemeAnalysis.Layouts"/>.</summary>
        private void FindLayouts()
        {
            // A language version is offered by its template, never on its own: it is not a layout to choose.
            foreach (var file in _files.Values
                         .Where(file => !file.IsAsset && !SiteThemePaths.IsLanguageVersion(file.Path))
                         .OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                if (file.SyntaxErrors.Count == 0
                    && !file.FrontMatter.Exists
                    && Reaches(file.Path, f => f.CallsSiteHead)
                    && Reaches(file.Path, f => f.UsesBody))
                {
                    Layouts.Add(file.Path);
                }
            }
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

        /// <summary>
        /// The theme's texts: a file that is not valid would be skipped by the renderer, so it is an error here, and
        /// so is a second file for a language the first already covers (<c>locales/ja.json</c> and
        /// <c>locales/JA.json</c>), of which the renderer could keep only one.
        /// </summary>
        private void ReportTextFiles()
        {
            var covered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in _files.Values
                         .Where(file => file.TextFile != null)
                         .OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                var result = file.TextFile!;
                if (!result.IsValid)
                {
                    Error(SiteThemeIssueCodes.TextFileInvalid, file.Path, null, null, ("Message", result.Error!));
                    continue;
                }

                if (covered.TryGetValue(result.CultureName!, out var first))
                {
                    Error(SiteThemeIssueCodes.TextFileDuplicate, file.Path, null, null,
                        ("Culture", result.CultureName!), ("Other", first));
                    continue;
                }

                covered[result.CultureName!] = file.Path;
            }
        }

        /// <summary>
        /// A language version (<c>Default.ja</c>) is rendered in place of its template, so it needs one: without
        /// <c>Default</c>, a page in Japanese would render by Scriban and the others by the Razor view of that name.
        /// </summary>
        private void ReportLanguageVersionsWithoutTemplate()
        {
            foreach (var path in _files.Keys.OrderBy(path => path, StringComparer.Ordinal))
            {
                if (SiteThemePaths.TryGetLanguageVersion(path, out var templatePath, out _)
                    && !IsTemplate(templatePath))
                {
                    Error(SiteThemeIssueCodes.LanguageVersionWithoutTemplate, path, null, null, ("Template", templatePath));
                }
            }
        }

        /// <summary>
        /// A file whose path is also the folder of others (<c>Products/Campus</c> beside <c>Products/Campus/Pricing</c>):
        /// saving, renaming and importing refuse to make one, but a working copy from before that, or a restored release,
        /// can hold it, and it would not publish into a theme that can be exported to a disk.
        /// </summary>
        private void ReportPathsThatAreFolders()
        {
            var paths = _files.Keys.OrderBy(path => path, StringComparer.Ordinal).ToList();
            foreach (var path in paths)
            {
                var below = paths.FirstOrDefault(other => other.Length > path.Length + 1
                                                          && other[path.Length] == '/'
                                                          && other.StartsWith(path, StringComparison.Ordinal));
                if (below != null)
                {
                    Error(SiteThemeIssueCodes.PathIsFolder, path, null, null, ("Other", below));
                }
            }
        }

        /// <summary>
        /// What a template is used as, its language versions are used as too: a page template's versions are page
        /// templates (and choose their own layout by their own front matter), an included template's are included,
        /// and the versions of every layout a page ends up in are layouts, so each is held to what a layout must do.
        /// Run once every role has been read.
        /// </summary>
        private void ReadLanguageVersions()
        {
            foreach (var pageTemplate in _pageTemplates.ToList())
            {
                foreach (var version in GetLanguageVersions(pageTemplate))
                {
                    _pageTemplates.Add(version);
                }
            }

            foreach (var pageTemplate in _pageTemplates)
            {
                if (GetEffectiveLayout(pageTemplate) is { } layout && IsTemplate(layout))
                {
                    _layouts.Add(layout);
                }
            }

            foreach (var included in _included.ToList())
            {
                foreach (var version in GetLanguageVersions(included))
                {
                    _included.Add(version);
                }
            }

            foreach (var layout in _layouts.ToList())
            {
                foreach (var version in GetLanguageVersions(layout))
                {
                    _layouts.Add(version);
                }
            }
        }

        /// <summary>The templates that are a language version of <paramref name="templatePath"/>.</summary>
        private IEnumerable<string> GetLanguageVersions(string templatePath)
        {
            if (!_languageVersions.TryGetValue(templatePath, out var versions))
            {
                return Array.Empty<string>();
            }

            return versions.Where(IsTemplate);
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
                ReportMissingArguments(source, reference);
            }

            AddUsage(target, new SiteThemeUsage(kind, source.Path, null));
        }

        /// <summary>
        /// An <c>include</c> passes a partial its locals, and only those: a partial that reads <c>$title</c> fails the render
        /// wherever it is included without <c>title:</c>. The template and every language version of it are held to it, since
        /// the include renders whichever one fits the page's language.
        /// </summary>
        private void ReportMissingArguments(SiteThemeFileAnalysis source, SiteThemeReference reference)
        {
            var passed = reference.NamedArguments ?? Array.Empty<string>();

            var missing = new[] { reference.Path }
                .Concat(GetLanguageVersions(reference.Path))
                .SelectMany(path => _files[path].RequiredLocals)
                .Where(name => !passed.Contains(name, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal);

            foreach (var name in missing)
            {
                Error(SiteThemeIssueCodes.IncludeArgumentMissing, source.Path, reference.Line, reference.Column,
                    ("Target", reference.Path), ("Variable", name));
            }
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
                // The theme's texts are read by `t`, and a language version is picked by its template: neither is
                // referred to by name. A language version of an unused template is not reported apart from it.
                if (!Usages.ContainsKey(path) && _files[path].TextFile == null && !SiteThemePaths.IsLanguageVersion(path))
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
