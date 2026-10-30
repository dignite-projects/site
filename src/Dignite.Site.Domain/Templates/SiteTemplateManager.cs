using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Templates;

/// <summary>
/// Creating and changing templates. Every body that is saved is parsed first, so a template that does not
/// parse never becomes a version - and the layout is held to one extra rule, that it calls
/// <c>site_head</c>.
/// </summary>
public class SiteTemplateManager : DomainService
{
    protected ISiteTemplateRepository TemplateRepository { get; }

    public SiteTemplateManager(ISiteTemplateRepository templateRepository)
    {
        TemplateRepository = templateRepository;
    }

    /// <summary>
    /// Creates a template with its first version. With <paramref name="publish"/> (the default) that
    /// version is live at once; without it the template is a draft that no page renders yet.
    /// </summary>
    public virtual async Task<SiteTemplate> CreateAsync(
        string name,
        string body,
        string? comment = null,
        bool publish = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = SiteTemplate.NormalizeName(name);

        if (await TemplateRepository.NameExistsAsync(normalizedName, null, cancellationToken))
        {
            throw new SiteTemplateNameAlreadyExistException(normalizedName);
        }

        CheckBody(normalizedName, body);

        var template = new SiteTemplate(GuidGenerator.Create(), normalizedName, CurrentTenant.Id);
        template.AddVersion(GuidGenerator.Create(), body, comment);

        if (publish)
        {
            template.Publish(1);
        }

        return await TemplateRepository.InsertAsync(template, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Saves <paramref name="body"/> as the next version of <paramref name="template"/>, which must have
    /// its versions loaded. With <paramref name="publish"/> (the default) the new version goes live;
    /// without it the edit is saved as a draft version and the live one is untouched.
    /// </summary>
    public virtual async Task<SiteTemplateVersion> AddVersionAsync(
        SiteTemplate template,
        string body,
        string? comment = null,
        bool publish = true,
        CancellationToken cancellationToken = default)
    {
        CheckBody(template.Name, body);

        var version = template.AddVersion(GuidGenerator.Create(), body, comment);

        if (publish)
        {
            template.Publish(version.Number);
        }

        await TemplateRepository.UpdateAsync(template, cancellationToken: cancellationToken);
        return version;
    }

    /// <summary>
    /// Points the template at <paramref name="versionNumber"/>, newer or older - rolling back is
    /// publishing an earlier version. The template must have its versions loaded.
    /// </summary>
    public virtual async Task PublishAsync(
        SiteTemplate template,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
        template.Publish(versionNumber);

        await TemplateRepository.UpdateAsync(template, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes the template. It is soft-deleted, which is an <c>UPDATE</c>, so the database's cascading
    /// foreign key does not fire: the versions are not removed by it and stay behind as unreachable
    /// history - they are only ever read through their template.
    /// </summary>
    public virtual async Task DeleteAsync(SiteTemplate template, CancellationToken cancellationToken = default)
    {
        await TemplateRepository.DeleteAsync(template, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The save-time gate. Syntax first, then the layout rule: the layout is the template every page
    /// renders inside, so one without <c>site_head</c> would silently drop the SEO head tags site-wide.
    /// </summary>
    protected virtual void CheckBody(string name, string body)
    {
        var errors = ScribanTemplateSyntax.GetErrors(body);
        if (errors.Count > 0)
        {
            throw new SiteTemplateSyntaxException(name, errors);
        }

        if (SiteTemplate.NormalizeName(name) == SiteTemplateConsts.LayoutName &&
            !ScribanTemplateSyntax.CallsSiteHead(ScribanTemplateSyntax.Parse(body)))
        {
            throw new SiteTemplateMissingSiteHeadException(name);
        }
    }
}
