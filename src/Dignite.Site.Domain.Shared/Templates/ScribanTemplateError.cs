namespace Dignite.Site.Templates;

/// <summary>One syntax error of a Scriban template. <paramref name="Line"/> and <paramref name="Column"/> are 1-based.</summary>
public sealed record ScribanTemplateError(int Line, int Column, string Message);
