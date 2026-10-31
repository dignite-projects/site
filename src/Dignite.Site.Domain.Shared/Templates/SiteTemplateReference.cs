namespace Dignite.Site.Templates;

/// <summary>
/// One place a template names another template: the function it does it with (<c>include</c> or
/// <c>asset_url</c>) and the normalized name of the template it points at. See
/// <see cref="ScribanTemplateSyntax.GetTemplateReferences"/>.
/// </summary>
public sealed record SiteTemplateReference(string FunctionName, string TemplateName);
