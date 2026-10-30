using Volo.Abp;

namespace Dignite.Site.Templates;

/// <summary>Publishing a version number the template does not have.</summary>
public class SiteTemplateVersionNotFoundException : BusinessException
{
    public SiteTemplateVersionNotFoundException(string name, int number)
        : base(SiteErrorCodes.SiteTemplateVersionNotFound)
    {
        WithData("Name", name);
        WithData("Number", number);
    }
}
