using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Sites.Pages;

public interface IPageRepository : IBasicRepository<Page, Guid>
{
    Task<Page?> FindByNameAsync(string name, bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the page whose <see cref="Page.Route"/> is exactly <paramref name="route"/> - step 1 of
    /// route resolution (总体设计 §3.4).
    /// </summary>
    Task<Page?> FindByRouteAsync(string route, bool includeDetails = false, CancellationToken cancellationToken = default);

    Task<bool> RouteExistsAsync(string route, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<Page?> FindHomePageAsync(bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every routable page, longest route first.
    /// <para>
    /// The ordering is the point, not a convenience: step 2 of route resolution matches a request path
    /// against route prefixes, and <c>/blog</c> is a prefix of <c>/blog-archive</c>. Testing longer
    /// routes first is what stops the shorter page from swallowing the longer one's URLs.
    /// </para>
    /// </summary>
    Task<List<Page>> GetRoutableListAsync(CancellationToken cancellationToken = default);

    Task<List<Page>> GetListAsync(
        bool? isActive = null,
        string? filter = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        string? sorting = null,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(bool? isActive = null, string? filter = null, CancellationToken cancellationToken = default);
}
