using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Themes;

namespace Dignite.Site.Admin.Themes;

internal static class SiteThemeSyntaxErrors
{
    /// <summary>The syntax errors of a template's body, as the administration API reports them.</summary>
    public static List<SiteThemeSyntaxErrorDto> Of(string body) =>
        ScribanTemplateSyntax.GetErrors(body)
            .Select(error => new SiteThemeSyntaxErrorDto { Line = error.Line, Column = error.Column, Message = error.Message })
            .ToList();
}
