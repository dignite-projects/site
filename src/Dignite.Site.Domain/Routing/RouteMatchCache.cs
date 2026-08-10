using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dignite.Site.Routing;

/// <summary>
/// Memoizes <see cref="SiteRouteResolver.ResolveAsync"/> for the lifetime of the current
/// <see cref="IUnitOfWork"/> - not the DI scope. A DI scope can span several logically-independent
/// operations with data changes in between (a test method that resolves, mutates a page, then resolves
/// again and expects the mutation reflected; a long-lived background scope that runs several unrelated
/// generations back to back), where a scope-lifetime cache would wrongly keep serving the first operation's
/// answer to the second. A UnitOfWork boundary is the one this codebase already treats as "one logical
/// operation" - the same boundary <c>RoutingPublicAppService</c>/<c>ContentPublicAppService</c> already
/// reason about for their shared-DbContext sequencing - and ASP.NET Core's default UOW middleware starts a
/// fresh one per HTTP request, which is exactly the granularity a single page render needs.
/// <para>
/// <see cref="SiteRouteResolver"/> itself is a Transient <c>DomainService</c> with no shared state of its
/// own, and a single content-page render currently resolves the same path twice with provably identical
/// arguments - once from <c>RoutingPublicAppService</c>, once independently from
/// <c>HeadMetadataPublicAppService</c> - each paying for a fresh, uncached walk of the routing table
/// (<c>IPageRepository.GetRoutableListAsync</c>) plus a slug lookup.
/// </para>
/// <para>
/// <b>Thread-safe despite <see cref="IUnitOfWork.Items"/> being a plain, non-concurrent
/// <see cref="Dictionary{TKey,TValue}"/>.</b> The lock below only ever guards the synchronous "does this key
/// already have a <see cref="Lazy{T}"/> slot, and if not, create and store one" step - never
/// <paramref name="resolveAsync"/> itself, which always runs and is awaited outside the lock. Two callers
/// racing for the same key both get the same <see cref="Lazy{T}"/>, so the underlying resolve runs exactly
/// once either way; two callers for different keys still serialize briefly on this one lock, but the
/// critical section is a dictionary lookup, not I/O, so that cost is negligible.
/// </para>
/// </summary>
public class RouteMatchCache : ITransientDependency
{
    // One IUnitOfWork.Items entry, not one entry per (path, cultureName, includeUnpublished) - the entry's
    // own value is the lookup table, keyed by a ValueTuple rather than a hand-built delimited string, so
    // there is no delimiter to pick and nothing for two different inputs to collide across.
    private const string ItemKey = "Dignite.Site.RouteMatchCache";

    // Static and shared by every instance/request on purpose: it only ever protects the tiny synchronous
    // table lookup below, never resolveAsync itself, so cross-request contention on it is not a concern.
    private static readonly object SyncRoot = new();

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    public RouteMatchCache(IUnitOfWorkManager unitOfWorkManager)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// Returns the cached match for this exact (<paramref name="path"/>, <paramref name="cultureName"/>,
    /// <paramref name="includeUnpublished"/>) combination, resolving via <paramref name="resolveAsync"/>
    /// only on a miss. Resolves every time, uncached, when there is no ambient UnitOfWork to key the cache
    /// to. A failed resolution is not re-attempted within the same UnitOfWork - the same exception
    /// resurfaces for every later call with the same key, matching how <see cref="Lazy{T}"/> itself treats a
    /// faulted factory.
    /// </summary>
    public virtual async Task<RouteMatch> GetOrResolveAsync(
        string path,
        string cultureName,
        bool includeUnpublished,
        Func<Task<RouteMatch>> resolveAsync)
    {
        var uowItems = UnitOfWorkManager.Current?.Items;
        if (uowItems == null)
        {
            return await resolveAsync();
        }

        var key = (path, cultureName, includeUnpublished);
        Lazy<Task<RouteMatch>> lazyMatch;

        lock (SyncRoot)
        {
            var table = GetOrAddTable(uowItems);
            if (!table.TryGetValue(key, out lazyMatch!))
            {
                lazyMatch = new Lazy<Task<RouteMatch>>(resolveAsync, LazyThreadSafetyMode.ExecutionAndPublication);
                table[key] = lazyMatch;
            }
        }

        return await lazyMatch.Value;
    }

    private static Dictionary<(string Path, string CultureName, bool IncludeUnpublished), Lazy<Task<RouteMatch>>> GetOrAddTable(
        IDictionary<string, object> uowItems)
    {
        if (uowItems.TryGetValue(ItemKey, out var existing))
        {
            return (Dictionary<(string, string, bool), Lazy<Task<RouteMatch>>>)existing;
        }

        var table = new Dictionary<(string, string, bool), Lazy<Task<RouteMatch>>>();
        uowItems[ItemKey] = table;
        return table;
    }
}
