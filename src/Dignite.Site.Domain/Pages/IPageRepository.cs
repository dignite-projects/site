using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Site.Pages;

public interface IPageRepository : IBasicRepository<Page, Guid>
{
    Task<Page?> FindByNameAsync(string name, bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the page whose own address (<see cref="Page.GetPath"/>) is exactly <paramref name="path"/> -
    /// step 1 of route resolution (总体设计 §3.4). Not a literal match against <see cref="Page.Route"/>:
    /// that may be a template (<c>/blog/{slug}</c>), whose own address (<c>/blog</c>) is a derived,
    /// shorter string.
    /// </summary>
    Task<Page?> FindByPathAsync(string path, bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any page's own address (<see cref="Page.GetPath"/>) would collide with
    /// <paramref name="route"/>'s. Not a literal <see cref="Page.Route"/> comparison: <c>/blog</c> and
    /// <c>/blog/{slug}</c> are different strings that both claim the address <c>/blog</c>, and only one
    /// page may.
    /// </summary>
    Task<bool> RouteExistsAsync(string route, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<Page?> FindHomePageAsync(bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every routable page, ordered the same way <see cref="GetListAsync"/> is (by <see cref="Page.Route"/>)
    /// - a deterministic order, not one that disambiguates matches. Step 2 of route resolution matches a
    /// whole request path against each candidate's route as one anchored template, so two distinct
    /// templates cannot both match a path unless an admin has deliberately constructed overlapping ones -
    /// a case the engine does not try to resolve, only to answer consistently.
    /// </summary>
    Task<List<Page>> GetRoutableListAsync(CancellationToken cancellationToken = default);

    Task<List<Page>> GetListAsync(
        bool? isActive = null,
        string? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Every page whose id is in <paramref name="ids"/> - a batched lookup for a caller that already has a set of PageIds (e.g. building one Url per Content across a list) and needs their owning Pages without an N+1.</summary>
    Task<List<Page>> GetListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// The children of <paramref name="parentId"/> (null meaning the top-level pages), ordered the same
    /// way <see cref="GetListAsync"/> orders its whole result - by <see cref="Page.Route"/>. The Admin UI's
    /// page-tree read for one level.
    /// </summary>
    Task<List<Page>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken = default);

    Task<bool> AnyChildAsync(Guid parentId, CancellationToken cancellationToken = default);
}
