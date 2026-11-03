namespace Dignite.Site.Public.Scriban;

/// <summary>
/// What <see cref="ScribanPageRenderer.RenderAsync"/> produced: the page's HTML, the layout it was rendered in, if any,
/// and the page template that was used - which is the language version of the page's template, when it has one for the
/// page's language.
/// </summary>
public sealed record ScribanPageRender(string Html, string? LayoutPath, string? TemplatePath = null);
