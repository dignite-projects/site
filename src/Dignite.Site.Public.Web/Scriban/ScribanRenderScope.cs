using System;
using System.Collections.Generic;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Themes;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// What one page render's functions share: the page being rendered, the request, the theme it renders with, and
/// what the render has done so far.
/// </summary>
public class ScribanRenderScope
{
    public ScribanRenderScope(
        SiteRenderViewModel viewModel,
        ActionContext actionContext,
        SiteThemeRenderSet renderSet,
        string? defaultCultureName)
    {
        ViewModel = viewModel;
        ActionContext = actionContext;
        RenderSet = renderSet;
        DefaultCultureName = defaultCultureName;

        if (viewModel.Content != null)
        {
            foreach (var field in viewModel.Content.ContentType.Fields)
            {
                NamedFieldIds.Add(field.FieldId);
            }
        }
    }

    public SiteRenderViewModel ViewModel { get; }

    public ActionContext ActionContext { get; }

    /// <summary>
    /// The theme this render was started with - the live release, or what a preview renders - whose templates
    /// <c>include</c> reads and whose static files' hashes <c>asset_url</c> puts in an address. Fixed for the whole render.
    /// </summary>
    public SiteThemeRenderSet RenderSet { get; }

    /// <summary>
    /// The site's default language, the last one <c>t</c> tries. Handed in by the renderer, which loads the site's
    /// languages for the render, so that no function depends on someone else having loaded them first.
    /// </summary>
    public string? DefaultCultureName { get; }

    /// <summary>
    /// Set by <c>site_head</c>. A render that never set it shipped a page without its SEO head tags, which
    /// <see cref="ScribanPageRenderer"/> refuses.
    /// </summary>
    public bool SiteHeadCalled { get; set; }

    /// <summary>
    /// The pages <c>find_page</c> has looked up, by name; null for a name no page answers to. A page is fetched
    /// once per render however often a template asks for it.
    /// </summary>
    public Dictionary<string, PageDto?> PagesByName { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The field definitions <c>field_definitions</c> has fetched, by id; null for an id with no field. Like
    /// <see cref="PagesByName"/>, a definition is fetched once per render.
    /// </summary>
    public Dictionary<Guid, FieldDto?> FieldsById { get; } = new();

    /// <summary>
    /// The ids of the fields a content type this render has seen names: those of the current content's type, and
    /// those of every page <c>find_page</c> returned. <c>field_definitions</c> resolves only these, because the
    /// public field service is scoped to ids a content type already named, not to the whole field library.
    /// </summary>
    public HashSet<Guid> NamedFieldIds { get; } = new();
}
