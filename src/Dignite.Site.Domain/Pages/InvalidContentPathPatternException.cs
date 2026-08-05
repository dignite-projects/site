using Volo.Abp;

namespace Dignite.Site.Pages;

/// <summary>
/// A content path pattern was given that has no <c>{slug}</c> placeholder, which would route every
/// content beneath the page to the same URL.
/// </summary>
public class InvalidContentPathPatternException : BusinessException
{
    public InvalidContentPathPatternException(string pattern)
        : base(SiteErrorCodes.InvalidContentPathPattern)
    {
        WithData("Pattern", pattern);
    }
}
