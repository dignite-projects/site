using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Site.Seo;

/// <summary>
/// Answers "what absolute origin does this tenant's site live on" - the one input every derived document
/// needs before it can emit a single URL (总体设计 §4.2, §5.2).
/// <para>
/// An interface rather than a plain domain service purely so the web layer can add a fallback to the
/// incoming request's own scheme and host without <c>IHttpContextAccessor</c> having to exist down here.
/// The default implementation reads the setting and nothing else.
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
