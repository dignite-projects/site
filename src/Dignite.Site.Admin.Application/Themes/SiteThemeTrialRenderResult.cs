using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Themes;

namespace Dignite.Site.Admin.Themes;

/// <summary>What a run of <see cref="SiteThemeTrialRenderer"/> did and found.</summary>
public class SiteThemeTrialRenderResult
{
    // By reference: two findings with the same values are still two findings.
    private readonly Dictionary<SiteThemeIssue, List<string>> _templates = new(ReferenceEqualityComparer.Instance);

    public SiteThemeTrialRenderResult(SiteThemeRenderScope scope)
    {
        Scope = scope;
    }

    /// <summary>Which pages the run was asked to render: all of the sample, or those a changed file takes part in.</summary>
    public SiteThemeRenderScope Scope { get; }

    /// <summary>The failures, and a warning when the run was cut short or could not render.</summary>
    public List<SiteThemeIssue> Issues { get; } = new();

    /// <summary>The page templates (language versions apart) whose every sample address the run rendered.</summary>
    public List<string> Templates { get; } = new();

    /// <summary>
    /// The page templates whose sample addresses met <paramref name="issue"/> - every one of them, though the finding is
    /// reported once: a partial that fails fails every page that includes it. Empty for a finding about the run as a whole.
    /// </summary>
    public IReadOnlyList<string> GetTemplates(SiteThemeIssue issue)
    {
        return _templates.TryGetValue(issue, out var templates) ? templates : Array.Empty<string>();
    }

    public void AddTemplate(SiteThemeIssue issue, string template)
    {
        if (!_templates.TryGetValue(issue, out var templates))
        {
            _templates[issue] = templates = new List<string>();
        }

        if (!templates.Contains(template, StringComparer.Ordinal))
        {
            templates.Add(template);
        }
    }
}
