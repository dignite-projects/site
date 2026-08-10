using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dignite.Site.Seo;

/// <summary>
/// Memoizes <see cref="SiteUrlBuilder.CreateContextAsync"/> for the lifetime of the current
/// <see cref="IUnitOfWork"/> - see <c>RouteMatchCache</c>'s own doc comment for why a UnitOfWork boundary,
/// not the DI scope, is the right one. A single request currently builds this three to four times with an
/// identical result - once each from <c>RoutingPublicAppService</c>, <c>HeadMetadataPublicAppService</c>,
/// its internal <c>HeadMetadataBuilder</c>, and - for a list page - <c>ContentPublicAppService.GetListAsync</c>
/// - each paying for the underlying Setting reads again.
/// <para>
/// <b>Thread-safe despite <see cref="IUnitOfWork.Items"/> being a plain, non-concurrent
/// <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/></b> - see <c>RouteMatchCache</c>'s own
/// doc comment for the exact mechanism (a lock around only the synchronous "get or create the
/// <see cref="Lazy{T}"/> slot" step, never around <paramref name="createAsync"/>'s own <c>this.Value</c> in
/// <see cref="GetOrCreateAsync"/>).
/// </para>
/// </summary>
public class SiteUrlContextCache : ITransientDependency
{
    private const string ItemKey = "Dignite.Site.SiteUrlContextCache";

    // Static and shared by every instance/request on purpose - see RouteMatchCache's own SyncRoot for why.
    private static readonly object SyncRoot = new();

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    public SiteUrlContextCache(IUnitOfWorkManager unitOfWorkManager)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// Returns the cached context, building it via <paramref name="createAsync"/> only on the first call
    /// within the current UnitOfWork. Builds every time, uncached, when there is no ambient UnitOfWork to
    /// key the cache to. A failed build is not re-attempted within the same UnitOfWork - the same exception
    /// resurfaces for every later call, matching how <see cref="Lazy{T}"/> itself treats a faulted factory.
    /// </summary>
    public virtual async Task<SiteUrlContext> GetOrCreateAsync(Func<Task<SiteUrlContext>> createAsync)
    {
        var uowItems = UnitOfWorkManager.Current?.Items;
        if (uowItems == null)
        {
            return await createAsync();
        }

        Lazy<Task<SiteUrlContext>> lazyContext;

        lock (SyncRoot)
        {
            if (uowItems.TryGetValue(ItemKey, out var existing))
            {
                lazyContext = (Lazy<Task<SiteUrlContext>>)existing;
            }
            else
            {
                lazyContext = new Lazy<Task<SiteUrlContext>>(createAsync, LazyThreadSafetyMode.ExecutionAndPublication);
                uowItems[ItemKey] = lazyContext;
            }
        }

        return await lazyContext.Value;
    }
}
