using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Pages;
using Dignite.Site.Templates;
using Volo.Abp;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// One place a template names another: the template whose current version does it, and the function it does
/// it with (<c>include</c> or <c>asset_url</c>).
/// </summary>
public sealed record SiteTemplateUsageReference(SiteTemplateCurrentVersion Source, string FunctionName);

/// <summary>
/// Who uses which template, worked out once by <see cref="SiteTemplateUsageCalculator"/> from the routable
/// pages and every template's current version, then asked as many times as a caller needs - a list asks it
/// once per row. Immutable: a snapshot of the moment it was calculated.
/// <para>
/// Names are compared ordinally, in their stored (normalized) form, which is how they are spelled - a page's
/// <c>Template</c> and a template's name share one normalization (<see cref="SiteTemplateNames.Normalize"/>).
/// </para>
/// </summary>
public class SiteTemplateUsageGraph
{
    /// <summary>
    /// How deep a chain of partials including partials is followed. A real site nests a handful at most; the
    /// cap only stops an accidental cycle the visited set does not already catch from costing anything.
    /// </summary>
    public const int MaxReferenceDepth = 10;

    private static readonly IReadOnlyList<SiteTemplateUsageReference> NoReferences = Array.Empty<SiteTemplateUsageReference>();
    private static readonly IReadOnlyList<Page> NoPages = Array.Empty<Page>();

    private readonly Dictionary<string, SiteTemplateCurrentVersion> _versionsByName;
    private readonly Dictionary<string, List<Page>> _pagesByTemplateName;
    private readonly Dictionary<string, IReadOnlyList<SiteTemplateUsageReference>> _referencesByTarget;

    /// <param name="pages">The routable pages, in the order every answer lists them in.</param>
    /// <param name="currentVersions">One row per template.</param>
    /// <param name="referencesByTarget">Target name -&gt; the templates that name it, one entry per source template.</param>
    public SiteTemplateUsageGraph(
        IReadOnlyList<Page> pages,
        IReadOnlyList<SiteTemplateCurrentVersion> currentVersions,
        IReadOnlyDictionary<string, IReadOnlyList<SiteTemplateUsageReference>> referencesByTarget)
    {
        Pages = Check.NotNull(pages, nameof(pages));
        CurrentVersions = Check.NotNull(currentVersions, nameof(currentVersions));
        Check.NotNull(referencesByTarget, nameof(referencesByTarget));

        _versionsByName = new Dictionary<string, SiteTemplateCurrentVersion>(StringComparer.Ordinal);
        foreach (var version in currentVersions)
        {
            _versionsByName[version.TemplateName] = version;
        }

        _pagesByTemplateName = new Dictionary<string, List<Page>>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            var templateName = SiteTemplateNames.Normalize(page.Template);
            if (!_pagesByTemplateName.TryGetValue(templateName, out var list))
            {
                _pagesByTemplateName[templateName] = list = new List<Page>();
            }

            list.Add(page);
        }

        _referencesByTarget = new Dictionary<string, IReadOnlyList<SiteTemplateUsageReference>>(
            referencesByTarget, StringComparer.Ordinal);
    }

    /// <summary>The routable pages: the home page first, then by display name.</summary>
    public IReadOnlyList<Page> Pages { get; }

    /// <summary>Every template's current version - the published one, else the newest - ordered by name.</summary>
    public IReadOnlyList<SiteTemplateCurrentVersion> CurrentVersions { get; }

    /// <summary>The current version of the template called <paramref name="name"/>, or null when there is no such template.</summary>
    public SiteTemplateCurrentVersion? FindCurrentVersion(string name)
    {
        return _versionsByName.GetValueOrDefault(name);
    }

    /// <summary>The pages whose <c>Template</c> is <paramref name="pageTemplateName"/>, in <see cref="Pages"/> order.</summary>
    public IReadOnlyList<Page> GetPagesUsing(string pageTemplateName)
    {
        return _pagesByTemplateName.TryGetValue(pageTemplateName, out var pages) ? pages : NoPages;
    }

    /// <summary>
    /// The templates whose current version names <paramref name="name"/> with <c>include</c> or <c>asset_url</c>
    /// - direct references only, one per source template, ordered by the source's name.
    /// </summary>
    public IReadOnlyList<SiteTemplateUsageReference> GetReferencesTo(string name)
    {
        return _referencesByTarget.GetValueOrDefault(name) ?? NoReferences;
    }

    /// <summary>
    /// The pages <paramref name="name"/> takes part in rendering, in <see cref="Pages"/> order: for a page
    /// template the pages using it; for the layout every page; for a partial or an asset the pages of every
    /// template that names it, followed through partials that include partials (at most
    /// <see cref="MaxReferenceDepth"/> deep). A reference from the layout therefore reaches every page.
    /// </summary>
    public IReadOnlyList<Page> GetPagesRendering(string name)
    {
        var pageIds = new HashSet<Guid>();
        CollectPages(name, pageIds, new HashSet<string>(StringComparer.Ordinal), depth: 0);

        return pageIds.Count == 0 ? NoPages : Pages.Where(page => pageIds.Contains(page.Id)).ToList();
    }

    /// <summary>
    /// The number a template list shows next to <paramref name="name"/>: for a page template the pages using
    /// it, for a partial or an asset the templates that name it, and null for the layout, which every page uses.
    /// </summary>
    public int? GetUsageCount(string name)
    {
        return SiteTemplateNames.GetKind(name) switch
        {
            SiteTemplateKind.Layout => null,
            SiteTemplateKind.Page => GetPagesUsing(name).Count,
            _ => GetReferencesTo(name).Count
        };
    }

    private void CollectPages(string name, HashSet<Guid> pageIds, HashSet<string> visited, int depth)
    {
        if (!visited.Add(name) || pageIds.Count == Pages.Count)
        {
            return;
        }

        switch (SiteTemplateNames.GetKind(name))
        {
            case SiteTemplateKind.Layout:
                pageIds.UnionWith(Pages.Select(page => page.Id));
                return;

            case SiteTemplateKind.Page:
                pageIds.UnionWith(GetPagesUsing(name).Select(page => page.Id));
                return;

            default:
                if (depth >= MaxReferenceDepth)
                {
                    return;
                }

                foreach (var reference in GetReferencesTo(name))
                {
                    CollectPages(reference.Source.TemplateName, pageIds, visited, depth + 1);
                }

                return;
        }
    }
}
