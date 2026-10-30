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

    /// <summary>Every template, ordered by name. <paramref name="filter"/> matches a name that contains it, ignoring case.</summary>
    Task<List<SiteTemplate>> GetListAsync(string? filter = null, bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// The live version of the template called <paramref name="name"/>, or null when there is no such
    /// template or it has never been published. Joins the version to its template's pointer instead of
    /// loading the template with all its versions: this runs on every page render, and a template's
    /// history grows without bound.
    /// </summary>
    Task<SiteTemplateVersion?> FindPublishedVersionAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The highest version number of each of the given templates, in one query - what a list needs to show
    /// "latest vN" next to the published pointer without loading every version. A template with no
    /// versions is absent from the result.
    /// </summary>
    Task<Dictionary<Guid, int>> GetLatestVersionNumbersAsync(IEnumerable<Guid> templateIds, CancellationToken cancellationToken = default);
}
