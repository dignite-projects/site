using System;
using System.Collections.Generic;
using Dignite.Site.Public.Templates;
using Dignite.Site.Templates;
using Volo.Abp;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// Puts one template version into a page's published render set, in the slot its name says it fills - the one rule
/// both previews follow: the site's draft mode (<see cref="SiteTemplatePreviewState"/>) and the administration
/// side's JSON preview (<see cref="Scriban.ScribanTemplatePreviewRenderer"/>).
/// </summary>
public static class SiteTemplatePreviewOverride
{
    /// <summary>
    /// A new render set: <paramref name="published"/> with <paramref name="version"/> of the template
    /// <paramref name="overrideName"/> in its place, by the name's kind -
    /// <list type="bullet">
    /// <item>the layout replaces <see cref="SiteTemplateRenderSetDto.Layout"/>; the page's template stays as it is,
    /// null included (a page still rendered by Razor cannot be wrapped in a Scriban layout);</item>
    /// <item>a page template replaces <see cref="SiteTemplateRenderSetDto.Template"/> only when it is the page's own
    /// template, <paramref name="pageTemplateName"/> - other pages do not use it;</item>
    /// <item>a partial takes its entry in <see cref="SiteTemplateRenderSetDto.Partials"/>, added when it was never
    /// published, so every <c>include</c> of it renders the version;</item>
    /// <item>an asset's entry in <see cref="SiteTemplateRenderSetDto.Assets"/> becomes the version's id, so
    /// <c>asset_url</c> writes an address no published version has.</item>
    /// </list>
    /// Always a new DTO with its own dictionaries, even when nothing changes: the published one may be shared by an
    /// in-process application service, and must never be changed.
    /// </summary>
    public static SiteTemplateRenderSetDto Apply(
        SiteTemplateRenderSetDto published,
        string pageTemplateName,
        string overrideName,
        SiteTemplateVersionDto version)
    {
        Check.NotNull(published, nameof(published));
        Check.NotNull(pageTemplateName, nameof(pageTemplateName));
        Check.NotNull(overrideName, nameof(overrideName));
        Check.NotNull(version, nameof(version));

        var renderSet = new SiteTemplateRenderSetDto
        {
            Template = published.Template,
            Layout = published.Layout,
            Partials = new Dictionary<string, SiteTemplateVersionDto>(published.Partials, published.Partials.Comparer),
            Assets = new Dictionary<string, Guid>(published.Assets, published.Assets.Comparer)
        };

        var name = SiteTemplateNames.Normalize(overrideName);

        switch (SiteTemplateNames.GetKind(name))
        {
            case SiteTemplateKind.Layout:
                renderSet.Layout = version;
                break;
            case SiteTemplateKind.Page:
                // Ordinal, like the lookup that found the published template by this name.
                if (string.Equals(name, SiteTemplateNames.Normalize(pageTemplateName), StringComparison.Ordinal))
                {
                    renderSet.Template = version;
                }

                break;
            case SiteTemplateKind.Partial:
                renderSet.Partials[name] = version;
                break;
            case SiteTemplateKind.Asset:
                renderSet.Assets[name] = version.Id;
                break;
        }

        return renderSet;
    }
}
