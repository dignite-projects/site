using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Dignite.Site.Templates;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Works out who uses which template (<see cref="SiteTemplateUsageGraph"/>): which routable pages name a page
/// template, and which templates name a partial or an asset. Two queries - the routable pages and one version
/// per template (<see cref="ISiteTemplateRepository.GetCurrentVersionsAsync"/>) - then every non-asset body is
/// parsed and its syntax tree walked (<see cref="ScribanTemplateSyntax.GetTemplateReferences"/>), so a name in
/// plain markup, a comment or an unrelated string does not count, and a name computed at run time cannot.
/// <para>
/// The current version of a template is its published one, or its newest when it was never published: what a
/// template refers to <i>now</i>, including a draft-only template its author is about to publish. A template's
/// older versions are history and refer to nothing.
/// </para>
/// </summary>
public class SiteTemplateUsageCalculator : ITransientDependency
{
    protected IPageRepository PageRepository { get; }

    protected ISiteTemplateRepository TemplateRepository { get; }

    public SiteTemplateUsageCalculator(IPageRepository pageRepository, ISiteTemplateRepository templateRepository)
    {
        PageRepository = pageRepository;
        TemplateRepository = templateRepository;
    }

    public virtual async Task<SiteTemplateUsageGraph> CalculateAsync(CancellationToken cancellationToken = default)
    {
        var pages = OrderPages(await PageRepository.GetRoutableListAsync(cancellationToken));
        var currentVersions = await TemplateRepository.GetCurrentVersionsAsync(cancellationToken);

        var referencesByTarget = new Dictionary<string, List<SiteTemplateUsageReference>>(StringComparer.Ordinal);

        foreach (var version in currentVersions)
        {
            // One entry per (source, target): a partial included three times by one template is one use of it.
            var targetsOfThisSource = new HashSet<string>(StringComparer.Ordinal);

            foreach (var reference in GetReferences(version))
            {
                if (!targetsOfThisSource.Add(reference.TemplateName))
                {
                    continue;
                }

                if (!referencesByTarget.TryGetValue(reference.TemplateName, out var sources))
                {
                    referencesByTarget[reference.TemplateName] = sources = new List<SiteTemplateUsageReference>();
                }

                sources.Add(new SiteTemplateUsageReference(version, reference.FunctionName));
            }
        }

        return new SiteTemplateUsageGraph(
            pages,
            currentVersions,
            referencesByTarget.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<SiteTemplateUsageReference>)pair.Value
                    .OrderBy(reference => reference.Source.TemplateName, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal));
    }

    /// <summary>
    /// The names <paramref name="version"/> refers to. None for an asset: its body is CSS or JS, never parsed
    /// as Scriban (<c>SiteTemplateManager</c> does not check it either), and not even read
    /// (<see cref="SiteTemplateCurrentVersion.Body"/> is empty).
    /// </summary>
    protected virtual IReadOnlyList<SiteTemplateReference> GetReferences(SiteTemplateCurrentVersion version)
    {
        if (SiteTemplateNames.IsAsset(version.TemplateName) || string.IsNullOrEmpty(version.Body))
        {
            return Array.Empty<SiteTemplateReference>();
        }

        return ScribanTemplateSyntax.GetTemplateReferences(
            ScribanTemplateSyntax.Parse(version.Body, version.TemplateName));
    }

    /// <summary>
    /// The order every answer lists pages in: the home page first - the address a preview most often starts
    /// from - then by display name, with the route breaking ties so the order is stable.
    /// </summary>
    protected virtual List<Page> OrderPages(IEnumerable<Page> pages)
    {
        return pages
            .OrderBy(page => page.IsHomeRoute() ? 0 : 1)
            .ThenBy(page => page.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(page => page.Route, StringComparer.Ordinal)
            .ToList();
    }
}
