using System.Threading.Tasks;
using Dignite.Site.Templates;

namespace Dignite.Site.Public.Templates;

public class SiteTemplatePublicAppService : SitePublicAppService, ISiteTemplatePublicAppService
{
    protected ISiteTemplateRepository TemplateRepository { get; }

    public SiteTemplatePublicAppService(ISiteTemplateRepository templateRepository)
    {
        TemplateRepository = templateRepository;
    }

    public virtual async Task<SiteTemplateRenderSetDto> GetRenderSetAsync(string name)
    {
        var template = await TemplateRepository.FindPublishedVersionAsync(SiteTemplate.NormalizeName(name));
        var layout = await TemplateRepository.FindPublishedVersionAsync(SiteTemplateConsts.LayoutName);

        return new SiteTemplateRenderSetDto
        {
            Template = template == null ? null : ObjectMapper.Map<SiteTemplateVersion, SiteTemplateVersionDto>(template),
            Layout = layout == null ? null : ObjectMapper.Map<SiteTemplateVersion, SiteTemplateVersionDto>(layout)
        };
    }
}
