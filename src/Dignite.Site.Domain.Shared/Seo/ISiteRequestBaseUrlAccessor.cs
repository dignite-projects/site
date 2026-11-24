using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Seo;

/// <summary>
/// The scheme, host and path base of the request being served, which the base URL falls back to when the
/// <c>Site.PrimaryDomain</c> setting is empty. Declared down here, with a default that knows of no request,
/// so the layer that does see the request (<c>Dignite.Site.Public.Web</c>) can answer without the domain
/// depending on ASP.NET Core: the same shape as ABP's <c>ICurrentPrincipalAccessor</c>.
/// </summary>
public interface ISiteRequestBaseUrlAccessor
{
    /// <summary>The request's base URL, such as <c>https://acme.example</c>, or <see langword="null"/> outside a request.</summary>
    string? GetBaseUrl();
}

[Dependency(TryRegister = true)]
public class NullSiteRequestBaseUrlAccessor : ISiteRequestBaseUrlAccessor, ISingletonDependency
{
    public string? GetBaseUrl() => null;
}
