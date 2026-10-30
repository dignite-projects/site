using Volo.Abp;

namespace Dignite.Site.Templates;

/// <summary>
/// A template's name is the handle <c>Page.Template</c> finds it by, so it has to be unique per tenant.
/// </summary>
public class SiteTemplateNameAlreadyExistException : BusinessException
{
    public SiteTemplateNameAlreadyExistException(string name)
        : base(SiteErrorCodes.SiteTemplateNameAlreadyExists)
    {
        WithData("Name", name);
    }
}
