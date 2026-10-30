using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.Site.Templates;

/// <summary>
/// The source of a template does not parse. Carries the individual errors, so a caller with an editor can
/// point at the lines; the localized message lists them for one that cannot.
/// </summary>
public class SiteTemplateSyntaxException : BusinessException
{
    public SiteTemplateSyntaxException(string name, IReadOnlyList<ScribanTemplateError> errors)
        : base(SiteErrorCodes.SiteTemplateSyntaxError)
    {
        Errors = errors;

        WithData("Name", name);
        WithData("Errors", ScribanTemplateSyntax.FormatErrors(errors));
    }

    public IReadOnlyList<ScribanTemplateError> Errors { get; }
}
