namespace Dignite.Site.Public.Scriban;

/// <summary>What <see cref="ScribanPageRenderer.RenderAsync"/> produced: the page's HTML, and the layout it was rendered in, if any.</summary>
public sealed record ScribanPageRender(string Html, string? LayoutPath);
