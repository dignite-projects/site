using Dignite.Site.Public.Routing;
using Microsoft.AspNetCore.Mvc.Razor;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public;

/// <summary>
/// Prefers a view location rooted at <c>/Site/...</c> before falling back to the framework's normal
/// candidate locations, so this app's own views live under one dedicated folder instead of the
/// conventional <c>/Views</c>/<c>/Pages</c> roots. When a tenant is resolved, that prefix becomes
/// <c>/Site/{tenantName}/...</c> instead of the plain <c>/Site/...</c> one - so a tenant can override
/// any <c>.cshtml</c> the current lookup would otherwise resolve by placing a file at the same relative
/// path under its own tenant-named folder. The two prefixed forms are mutually exclusive per request -
/// a tenant request never also tries the plain <c>/Site/...</c> tier - and both fall through to the
/// unmodified candidate locations when nothing matches, so this is purely additive - safe to have
/// registered even for a host that never ends up using the override.
/// <para>
/// Registered by <see cref="SitePublicWebModule"/> - a consumer only needs the module dependency, not its
/// own <c>Configure&lt;RazorViewEngineOptions&gt;</c>.
/// </para>
/// </summary>
public class TenantViewLocationExpander : IViewLocationExpander
{
    private const string TenantNameKey = "site_tenant_name";

    private readonly Lazy<ICurrentTenant?> _currentTenantLazy;

    public TenantViewLocationExpander(Lazy<ICurrentTenant?> currentTenantLazy)
    {
        _currentTenantLazy = currentTenantLazy;
    }

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var currentTenant = _currentTenantLazy.Value;
        if (currentTenant is { IsAvailable: true })
        {
            var tenantName = currentTenant.Name;
            if (!string.IsNullOrEmpty(tenantName))
            {
                context.Values[TenantNameKey] = tenantName;
            }
        }
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (context.AreaName is not null || context.ControllerName != SiteRenderController.ControllerName)
        {
            return viewLocations;
        }


        // Operates as a transform over whatever locations the framework (and any earlier-registered
        // expander) already produced - for Areas, Razor Pages, or plain MVC alike - rather than
        // hardcoding a parallel copy of the default view location formats that could drift out of sync
        // with them.
        context.Values.TryGetValue(TenantNameKey, out var tenantName);
        var baseLocations = viewLocations.ToList();
        var tenantSegment = string.IsNullOrEmpty(tenantName) ? string.Empty : $"/{tenantName}";

        var expanded = baseLocations
            .Where(location => location.StartsWith("/Views/"))
            .Select(location => $"/Site{tenantSegment}{location.RemovePreFix("/Views").RemovePreFix("/{1}")}")
            .ToList();
        expanded.AddRange(baseLocations);
        return expanded;
    }
}
