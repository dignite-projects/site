using System;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A failure that is the theme's, found by the renderer itself rather than by the engine: a template that does not
/// parse (<see cref="ScribanTemplateCache"/>), a layout that is not a template of the theme
/// (<see cref="ScribanPageRenderer"/>), or output past <see cref="SiteScribanOptions.MaxOutputLength"/>
/// (<see cref="BoundedScriptOutput"/>). The message names the template.
/// <para>
/// Its own type, so that a preview (<see cref="SiteThemePreviewRenderer"/>, <see cref="ScribanPageResult.ShowsErrors"/>)
/// can show the theme's author exactly these, and not every <see cref="InvalidOperationException"/> a render may meet
/// - a disposed service, a DbContext used twice at once - as if the template were at fault. It still is an
/// <see cref="InvalidOperationException"/>, for a caller that handled it as one.
/// </para>
/// </summary>
public class ScribanRenderException : InvalidOperationException
{
    public ScribanRenderException(string message)
        : base(message)
    {
    }

    public ScribanRenderException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
