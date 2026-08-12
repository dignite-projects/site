using Volo.Abp;

namespace Dignite.Site;

/// <summary>
/// A value was given for a "shaped identifier" property - Page.Name/Route/Template, ContentType.Name,
/// Field.Name, Content.Slug - that does not match the shape that property requires. Reused across all of
/// them rather than one exception per property, since the recovery is always the same: fix the value's
/// characters, nothing else varies.
/// </summary>
public class InvalidValueFormatException : BusinessException
{
    public InvalidValueFormatException(string propertyName, string value)
        : base(SiteErrorCodes.InvalidValueFormat)
    {
        WithData("PropertyName", propertyName);
        WithData("Value", value);
    }
}
