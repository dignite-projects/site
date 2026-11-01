using System;
using System.Threading.Tasks;
using Dignite.Site.Public.Scriban;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Themes;

/// <summary>
/// The current tenant's live release, as a <see cref="SiteThemeRenderSet"/>. Each page view asks
/// <see cref="ISiteThemePublicAppService.GetLiveAsync"/> once, passing the release it already holds: while that is
/// still live the answer is a single id and the held render set is reused, parsed templates and all; after a publish
/// or a rollback the answer carries the new release, which replaces it. So a publish shows on the next request, in
/// every instance, with no cache to invalidate.
/// </summary>
public class SiteLiveThemeProvider : ITransientDependency
{
    protected ISiteThemePublicAppService ThemeAppService { get; }
    protected SiteLiveThemeCache Cache { get; }
    protected ICurrentTenant CurrentTenant { get; }

    public SiteLiveThemeProvider(ISiteThemePublicAppService themeAppService, SiteLiveThemeCache cache, ICurrentTenant currentTenant)
    {
        ThemeAppService = themeAppService;
        Cache = cache;
        CurrentTenant = currentTenant;
    }

    /// <summary>The live release, or null when the tenant has never published one.</summary>
    public virtual async Task<SiteThemeRenderSet?> GetAsync()
    {
        var tenantId = CurrentTenant.Id;
        var held = Cache.Find(tenantId);

        var live = await ThemeAppService.GetLiveAsync(held?.ReleaseId);
        if (live.ReleaseId == null)
        {
            Cache.Remove(tenantId);
            return null;
        }

        if (live.IsUnchanged && held != null)
        {
            return held;
        }

        if (live.Snapshot == null)
        {
            // Unchanged, but the held set was evicted between the two calls: ask again without one.
            live = await ThemeAppService.GetLiveAsync();
            if (live.Snapshot == null)
            {
                return null;
            }
        }

        var renderSet = new SiteThemeRenderSet(live.Snapshot);
        Cache.Set(tenantId, renderSet);
        return renderSet;
    }
}

/// <summary>
/// The live render set of each tenant, the latest one only. A private, size-bounded <see cref="MemoryCache"/>, like
/// <see cref="ScribanTemplateCache"/>: every tenant's theme passes through it, and it must not compete for a cache
/// the host configures for its own use.
/// </summary>
public class SiteLiveThemeCache : ISingletonDependency, IDisposable
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromHours(1);

    private readonly MemoryCache _cache;

    public SiteLiveThemeCache(IOptions<SiteScribanOptions> options)
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = options.Value.TemplateCacheSizeLimit });
    }

    public virtual SiteThemeRenderSet? Find(Guid? tenantId)
    {
        return _cache.TryGetValue(GetKey(tenantId), out SiteThemeRenderSet? renderSet) ? renderSet : null;
    }

    public virtual void Set(Guid? tenantId, SiteThemeRenderSet renderSet)
    {
        _cache.Set(GetKey(tenantId), renderSet, new MemoryCacheEntryOptions
        {
            Size = Math.Max(1, renderSet.Size),
            SlidingExpiration = SlidingExpiration
        });
    }

    public virtual void Remove(Guid? tenantId)
    {
        _cache.Remove(GetKey(tenantId));
    }

    protected virtual string GetKey(Guid? tenantId)
    {
        return tenantId?.ToString("N") ?? "host";
    }

    public void Dispose()
    {
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }
}
