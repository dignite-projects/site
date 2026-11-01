using System;
using System.Collections.Generic;

namespace Dignite.Site.Themes;

/// <summary>One syntax error of a Scriban template. <paramref name="Line"/> and <paramref name="Column"/> are 1-based.</summary>
public sealed record ScribanTemplateError(int Line, int Column, string Message);

/// <summary>
/// One place a template names another file with a string literal - see
/// <see cref="ScribanTemplateSyntax.GetReferences"/> and <see cref="ScribanTemplateSyntax.ReadFrontMatter"/>.
/// </summary>
/// <param name="FunctionName"><c>include</c>, <c>asset_url</c>, or <c>layout</c> for the front matter.</param>
/// <param name="Path">The normalized path it names, or <c>none</c> for <c>layout = "none"</c>.</param>
/// <param name="Line">1-based line of the literal.</param>
/// <param name="Column">1-based column of the literal.</param>
/// <param name="Start">0-based offset of the literal in the source, its opening quote included.</param>
/// <param name="Length">Length of the literal in the source, both quotes included.</param>
public sealed record SiteThemeReference(string FunctionName, string Path, int Line, int Column, int Start, int Length);

/// <summary>What a template's front matter says - see <see cref="ScribanTemplateSyntax.ReadFrontMatter"/>.</summary>
/// <param name="Exists">Whether the template has front matter at all.</param>
/// <param name="Layout">The <c>layout</c> it sets, or null when it sets none.</param>
/// <param name="Errors">What is wrong with it: anything but <c>layout = "..."</c>.</param>
public sealed record SiteTemplateFrontMatter(bool Exists, SiteThemeReference? Layout, IReadOnlyList<ScribanTemplateError> Errors)
{
    public static SiteTemplateFrontMatter Empty { get; } = new(false, null, Array.Empty<ScribanTemplateError>());
}
