using System.Linq;
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
        var normalized = SiteTemplate.NormalizeName(name);
        var rows = await TemplateRepository.GetPublishedRenderSetAsync(normalized);

        // The requested name only yields a Template when it is a page template. A partial or an asset never
        // renders a page, even when it is published: if a page's Template pointed at one and this returned
        // it, the renderer would output a bare fragment without site_head instead of falling through to the
        // Razor view. (Asking for `layout` is fine - it is a page-kind name - and then Template and Layout
        // are the same version.)
        var template = SiteTemplateNames.IsPage(normalized)
            ? rows.FirstOrDefault(r => r.TemplateName == normalized)
            : null;
        var layout = rows.FirstOrDefault(r => r.TemplateName == SiteTemplateConsts.LayoutName);

        return new SiteTemplateRenderSetDto
        {
            Template = template == null ? null : ToDto(template),
            Layout = layout == null ? null : ToDto(layout),
            Partials = rows
                .Where(r => SiteTemplateNames.IsPartial(r.TemplateName))
                .ToDictionary(r => r.TemplateName, ToDto),
            Assets = rows
                .Where(r => SiteTemplateNames.IsAsset(r.TemplateName))
                .ToDictionary(r => r.TemplateName, r => r.Id)
        };
    }

    public virtual async Task<SiteTemplateVersionDto?> FindPublishedAsync(string name)
    {
        var version = await TemplateRepository.FindPublishedVersionAsync(SiteTemplate.NormalizeName(name));

        return version == null ? null : ObjectMapper.Map<SiteTemplateVersion, SiteTemplateVersionDto>(version);
    }

    /// <summary>
    /// By hand: <see cref="PublishedSiteTemplateVersion"/> is a read-model record, not an entity, and an
    /// asset row carries no body (it comes out as an empty string, which no caller reads).
    /// </summary>
    protected virtual SiteTemplateVersionDto ToDto(PublishedSiteTemplateVersion row)
    {
        return new SiteTemplateVersionDto
        {
            Id = row.Id,
            TemplateId = row.TemplateId,
            Number = row.Number,
            Body = row.Body ?? string.Empty,
            Comment = row.Comment,
            CreationTime = row.CreationTime
        };
    }
}
