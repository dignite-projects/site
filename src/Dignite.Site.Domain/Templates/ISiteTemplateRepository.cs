using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Site.Templates;

public interface ISiteTemplateRepository : IBasicRepository<SiteTemplate, Guid>
{
    /// <summary>
    /// Finds a template by its exact (already normalized - see <see cref="SiteTemplate.NormalizeName"/>)
    /// name. <paramref name="includeDetails"/> defaults to true, unlike the other lookups here: the caller
    /// that finds a template by name is usually about to add a version to it, which needs
    /// <see cref="SiteTemplate.Versions"/> loaded.
    /// </summary>
    Task<SiteTemplate?> FindByNameAsync(string name, bool includeDetails = true, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every template, ordered by name. <paramref name="filter"/> matches a name that contains it, ignoring
    /// case; <paramref name="prefix"/> keeps only names that start with it (ordinal, after
    /// <see cref="SiteTemplateNames.Normalize"/>) - <c>partials/</c> lists the partials, <c>assets/</c> the assets.
    /// </summary>
    Task<List<SiteTemplate>> GetListAsync(string? filter = null, string? prefix = null, bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// The live version of the template called <paramref name="name"/>, or null when there is no such
    /// template or it has never been published. Joins the version to its template's pointer instead of
    /// loading the template with all its versions: this runs on every page render, and a template's
    /// history grows without bound.
    /// </summary>
    Task<SiteTemplateVersion?> FindPublishedVersionAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything one page render may need, in one query: the live version of <paramref name="name"/> and of
    /// <see cref="SiteTemplateConsts.LayoutName"/>, every live template under
    /// <see cref="SiteTemplateConsts.PartialsPrefix"/> with its body, and every live template under
    /// <see cref="SiteTemplateConsts.AssetsPrefix"/> without its body (<see cref="PublishedSiteTemplateVersion.Body"/>
    /// is null - the renderer only needs the version id to build an asset's address). A template that was
    /// never published is absent. Runs on every page view, hence one round trip and no history.
    /// </summary>
    Task<List<PublishedSiteTemplateVersion>> GetPublishedRenderSetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The highest version number of each of the given templates, in one query - what a list needs to show
    /// "latest vN" next to the published pointer without loading every version. A template with no
    /// versions is absent from the result.
    /// </summary>
    Task<Dictionary<Guid, int>> GetLatestVersionNumbersAsync(IEnumerable<Guid> templateIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// One row per template: its published version, or - for a template never published - its newest one
    /// (<see cref="SiteTemplateCurrentVersion"/>). What working out which templates refer to which reads: one
    /// version per template, never a history. An asset's body is not read (empty), since it is never parsed.
    /// Ordered by name.
    /// </summary>
    Task<List<SiteTemplateCurrentVersion>> GetCurrentVersionsAsync(CancellationToken cancellationToken = default);
}
