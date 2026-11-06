using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Localization;
using Volo.Abp;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Localization;

namespace Dignite.Site.Themes;

/// <summary>
/// One thing wrong with a theme package: a <see cref="SiteThemePackageProblemCodes"/> code, the zip entry it is in, and
/// the limit or extension it is about - the <c>{Entry}</c> and <c>{Value}</c> of the code's message.
/// </summary>
public sealed record SiteThemePackageProblem(string Code, string? Entry = null, string? Value = null);

/// <summary>
/// A theme package refused whole (<see cref="SiteErrorCodes.SiteThemePackageInvalid"/>), with every problem found.
/// The problems are codes, put into words when the error is sent, in the language of the request: ABP turns an
/// exception that is <see cref="ILocalizeErrorMessage"/> into its error message that way (as ABP Identity's
/// <c>AbpIdentityResultException</c> is), where a plain business exception's data would go into the message as it is.
/// </summary>
public class SiteThemePackageInvalidException : BusinessException, ILocalizeErrorMessage
{
    public IReadOnlyList<SiteThemePackageProblem> Problems { get; }

    public SiteThemePackageInvalidException(IReadOnlyList<SiteThemePackageProblem> problems)
        : base(SiteErrorCodes.SiteThemePackageInvalid, Describe(problems))
    {
        Problems = problems;
    }

    public virtual string LocalizeMessage(LocalizationContext context)
    {
        var localizer = context.LocalizerFactory.Create(typeof(SiteResource));
        var lines = Problems.Select(problem => localizer[problem.Code].Value
            .Replace("{Entry}", problem.Entry ?? string.Empty, StringComparison.Ordinal)
            .Replace("{Value}", problem.Value ?? string.Empty, StringComparison.Ordinal));

        return localizer[SiteErrorCodes.SiteThemePackageInvalid].Value
            .Replace("{Problems}", string.Join(Environment.NewLine, lines), StringComparison.Ordinal);
    }

    /// <summary>The problems for a log, not for a person: codes and the entries they are in.</summary>
    private static string Describe(IEnumerable<SiteThemePackageProblem> problems)
    {
        return "The theme package cannot be imported: " + string.Join("; ", problems.Select(problem =>
            problem.Code
            + (problem.Entry == null ? string.Empty : " " + problem.Entry)
            + (problem.Value == null ? string.Empty : " (" + problem.Value + ")")));
    }
}
