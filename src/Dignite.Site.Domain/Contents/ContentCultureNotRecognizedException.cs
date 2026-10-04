using Volo.Abp;

namespace Dignite.Site.Contents;

/// <summary>
/// A content was given a culture name .NET does not ship (<see cref="CultureNameNormalizer"/>). Thrown
/// where the culture first enters the domain, so the caller learns which argument to fix instead of
/// getting the normalizer's <see cref="System.ArgumentException"/> - which no API surface reports as
/// anything but an internal error.
/// </summary>
public class ContentCultureNotRecognizedException : BusinessException
{
    public ContentCultureNotRecognizedException(string cultureName)
        : base(SiteErrorCodes.ContentCultureNotRecognized)
    {
        WithData("CultureName", cultureName);
    }
}
