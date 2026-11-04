using System.Threading.Tasks;

namespace Dignite.Site.Settings;

/// <summary>
/// Writes one of Site's settings, the counterpart of the <c>ISettingProvider</c> every Site reads them through.
/// <para>
/// Writing is Setting Management's, a module that needs a database of its own and replaces ABP's setting definition store
/// with one that reads it. Site's domain does not depend on it, so that a layer that only reads Site's settings - the test
/// layers, a host that keeps its settings elsewhere - does not need that database; this is the seam. The implementation
/// is <c>Dignite.Site.SettingManagement</c>'s, which a host that wants a site's settings written loads, and the
/// service that writes through it fails to resolve without one.
/// </para>
/// </summary>
public interface ISiteSettingWriter
{
    /// <summary>
    /// Sets <paramref name="name"/> to <paramref name="value"/> for the current site: for the current tenant and
    /// kept as that tenant's own, or, when there is no tenant, as the value of every site that sets none of its own.
    /// </summary>
    Task SetAsync(string name, string value);
}
