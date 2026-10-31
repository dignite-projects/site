using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Uow;

namespace Dignite.Site.Public.Templates;

/// <summary>
/// Reads a version in any state for the site's draft mode - see <see cref="ISiteTemplatePreviewVersionProvider"/>
/// for why this is not part of <see cref="ISiteTemplatePublicAppService"/>.
/// <para>
/// A plain class rather than a <see cref="SitePublicAppService"/>: an application service is exposed as an HTTP
/// endpoint by convention, and a draft must never be. The mapper is the one the Public application services use
/// (<see cref="SitePublicAppService"/> sets its <c>ObjectMapperContext</c> to <see cref="SitePublicApplicationModule"/>).
/// It loads the template with all its versions, which is fine: only a visitor in draft mode - the template's
/// author - ever causes this read, and the rendering side asks at most once per request.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteTemplatePreviewVersionProvider), typeof(SiteTemplatePreviewVersionProvider))]
public class SiteTemplatePreviewVersionProvider : ISiteTemplatePreviewVersionProvider, ITransientDependency, IUnitOfWorkEnabled
{
    protected ISiteTemplateRepository TemplateRepository { get; }
    protected IObjectMapper<SitePublicApplicationModule> ObjectMapper { get; }

    public SiteTemplatePreviewVersionProvider(
        ISiteTemplateRepository templateRepository,
        IObjectMapper<SitePublicApplicationModule> objectMapper)
    {
        TemplateRepository = templateRepository;
        ObjectMapper = objectMapper;
    }

    public virtual async Task<SiteTemplateVersionDto?> FindVersionAsync(
        string name,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        var template = await TemplateRepository.FindByNameAsync(
            SiteTemplate.NormalizeName(name), includeDetails: true, cancellationToken);
        var version = template?.FindVersion(versionNumber);

        return version == null ? null : ObjectMapper.Map<SiteTemplateVersion, SiteTemplateVersionDto>(version);
    }
}
