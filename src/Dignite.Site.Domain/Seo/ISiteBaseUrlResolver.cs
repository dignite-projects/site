using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Site.Seo;

/// <summary>
/// Answers "what absolute origin does this tenant's site live on" - the one input every derived document
/// needs before it can emit a single URL (总体设计 §4.2, §5.2).
/// <para>
/// The default, <see cref="SettingSiteBaseUrlResolver"/>, reads the <c>Site.PrimaryDomain</c> setting and
/// falls back to the request being served; a host that maps tenants to addresses its own way replaces it.
/// </para>
/// </summary>
public interface ISiteBaseUrlResolver
{
    /// <summary>
    /// The absolute base URL with no trailing slash (<c>https://acme.com</c>, or
    /// <c>https://acme.com/site</c> when hosted under a path), or <see langword="null"/> when none can be
    /// determined.
    /// </summary>
    Task<string?> GetBaseUrlAsync(CancellationToken cancellationToken = default);
}
