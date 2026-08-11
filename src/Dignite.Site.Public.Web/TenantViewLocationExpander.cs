using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Razor;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public;

/// <summary>
/// Tries tenant-specific view locations before the host's normal ones, so a tenant can override any
/// <c>.cshtml</c> the current lookup would otherwise resolve, simply by placing a file at the same
/// relative path under <c>/Tenants/{tenantName}/...</c>. Falls through to the unmodified candidate
/// locations when no tenant-specific view exists, so this is purely additive - safe to have registered
/// even for a host that never ends up using the override.
/// <para>
/// Registered by <see cref="PublicWebModule"/> - a consumer only needs the module dependency, not its
/// own <c>Configure&lt;RazorViewEngineOptions&gt;</c>.
/// </para>
/// </summary>
public class TenantViewLocationExpander : IViewLocationExpander
{
    private const string TenancyNameKey = "dignite:tenancyName";

    private readonly Lazy<ICurrentTenant?> _currentTenantLazy;

    public TenantViewLocationExpander(Lazy<ICurrentTenant?> currentTenantLazy)
    {
        _currentTenantLazy = currentTenantLazy;
    }

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var currentTenant = _currentTenantLazy.Value;
        if (!context.Values.ContainsKey(TenancyNameKey) && currentTenant is { IsAvailable: true })
        {
            var tenantName = currentTenant.Name;
            if (!string.IsNullOrEmpty(tenantName))
            {
                context.Values[TenancyNameKey] = tenantName;
            }
        }
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        context.Values.TryGetValue(TenancyNameKey, out var tenantName);

        if (string.IsNullOrEmpty(tenantName))
        {
            return viewLocations;
        }

        // Operates as a transform over whatever locations the framework (and any earlier-registered
        // expander) already produced - for Areas, Razor Pages, or plain MVC alike - rather than
        // hardcoding a parallel copy of the default view location formats that could drift out of sync
        // with them.
        var baseLocations = viewLocations.ToList();
        var prefixed = new List<string>();

        foreach (var location in baseLocations)
        {
            prefixed.Add($"/Tenants/{tenantName}{location}");
        }

        prefixed.AddRange(baseLocations);
        return prefixed;
    }
}
